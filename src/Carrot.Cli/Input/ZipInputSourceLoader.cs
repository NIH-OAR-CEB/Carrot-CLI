using System.IO.Compression;
using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Microsoft.Extensions.Options;

namespace Carrot.Cli.Input;

/**************************************************************/
/// <summary>
/// Defines guarded ZIP expansion and deterministic document discovery from archive entries.
/// </summary>
/// <remarks>
/// Future guards reject traversal, absolute paths, nested ZIPs, excessive expansion, and
/// excessive compression ratios before exposing extracted files to downstream processing.
/// </remarks>
/// <seealso cref="IInputSourceLoader"/>
internal sealed class ZipInputSourceLoader : IInputSourceLoader
{
    #region implementation

    private readonly DocumentFormatCatalog _formats;
    private readonly CarrotCliOptions _options;

    /**************************************************************/
    /// <summary>Initializes guarded archive discovery with shared formats and safety limits.</summary>
    public ZipInputSourceLoader(DocumentFormatCatalog formats, IOptions<CarrotCliOptions> options)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(formats);
        ArgumentNullException.ThrowIfNull(options);
        _formats = formats;
        _options = options.Value;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Safely expands and discovers supported files from a ZIP archive.
    /// </summary>
    public async Task<OperationResult<InputBatch>> LoadAsync(
        string inputPath,
        bool recursive,
        CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        cancellationToken.ThrowIfCancellationRequested();

        var archivePath = Path.GetFullPath(inputPath);
        if (!File.Exists(archivePath))
        {
            return failure("input.zip.missing", $"ZIP archive not found: {archivePath}");
        }

        var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"carrot-cli-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(temporaryDirectory);
            using var archive = ZipFile.OpenRead(archivePath);

            var supportedEntries = new List<ZipArchiveEntry>();
            var messages = new List<OperationMessage>();
            long expandedBytes = 0;

            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = normalizeEntryPath(entry.FullName);

                if (string.IsNullOrEmpty(relativePath) || relativePath.EndsWith("/", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!isSafeRelativeEntry(relativePath))
                {
                    return await failAndCleanAsync(
                        temporaryDirectory,
                        "input.zip.unsafe-path",
                        $"ZIP entry has an unsafe path: {entry.FullName}").ConfigureAwait(false);
                }

                if (Path.GetExtension(relativePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    return await failAndCleanAsync(
                        temporaryDirectory,
                        "input.zip.nested",
                        $"Nested ZIP archives are not supported: {relativePath}").ConfigureAwait(false);
                }

                if (entry.Length > _options.MaximumFileSizeBytes && _formats.IsSupportedDocument(relativePath))
                {
                    messages.Add(warning(
                        "input.file.too-large",
                        $"Skipped {relativePath}: {entry.Length:N0} bytes exceeds the per-file limit."));
                    continue;
                }

                if (entry.Length > _options.MaximumExpandedArchiveBytes - expandedBytes)
                {
                    return await failAndCleanAsync(
                        temporaryDirectory,
                        "input.zip.expanded-limit",
                        $"ZIP expansion exceeds the {_options.MaximumExpandedArchiveBytes:N0}-byte limit.").ConfigureAwait(false);
                }

                expandedBytes += entry.Length;

                if (entry.CompressedLength == 0 && entry.Length > 0
                    || entry.CompressedLength > 0
                    && (double)entry.Length / entry.CompressedLength > _options.MaximumCompressionRatio)
                {
                    return await failAndCleanAsync(
                        temporaryDirectory,
                        "input.zip.compression-ratio",
                        $"ZIP entry exceeds the configured compression ratio: {relativePath}").ConfigureAwait(false);
                }

                if (_formats.IsSupportedDocument(relativePath)
                    && !DocumentFormatCatalog.IsOfficeTemporaryFile(relativePath))
                {
                    supportedEntries.Add(entry);
                    if (supportedEntries.Count > _options.MaximumInputFiles)
                    {
                        return await failAndCleanAsync(
                            temporaryDirectory,
                            "input.file.limit",
                            $"ZIP contains more than {_options.MaximumInputFiles:N0} supported documents.").ConfigureAwait(false);
                    }
                }
            }

            var orderedEntries = supportedEntries
                .OrderBy(entry => normalizeEntryPath(entry.FullName), StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => normalizeEntryPath(entry.FullName), StringComparer.Ordinal)
                .ToArray();

            if (orderedEntries.Length == 0)
            {
                messages.Add(error("input.zip.empty", $"No supported documents were found in {archivePath}."));
                await deleteTemporaryDirectoryAsync(temporaryDirectory).ConfigureAwait(false);
                return OperationResult<InputBatch>.Failure(messages);
            }

            var files = new List<SourceFile>(orderedEntries.Length);
            foreach (var entry in orderedEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = normalizeEntryPath(entry.FullName);
                var destinationPath = Path.GetFullPath(Path.Combine(
                    temporaryDirectory,
                    relativePath.Replace('/', Path.DirectorySeparatorChar)));

                // The normalized-prefix check remains in place as defense in depth after segment validation.
                var temporaryPrefix = temporaryDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!destinationPath.StartsWith(temporaryPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return await failAndCleanAsync(
                        temporaryDirectory,
                        "input.zip.unsafe-path",
                        $"ZIP entry escapes its temporary directory: {relativePath}").ConfigureAwait(false);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                await using (var source = entry.Open())
                await using (var destination = new FileStream(
                    destinationPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true))
                {
                    await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
                }

                files.Add(new SourceFile
                {
                    SourceOrdinal = files.Count,
                    SourceKey = $"{archivePath}::{relativePath}",
                    ContainerPath = archivePath,
                    PhysicalPath = destinationPath,
                    RelativePath = relativePath,
                    FileName = Path.GetFileName(relativePath),
                    Extension = Path.GetExtension(relativePath).ToLowerInvariant(),
                    SizeBytes = entry.Length
                });
            }

            var batch = new InputBatch
            {
                ContainerPath = archivePath,
                Files = files.AsReadOnly(),
                TemporaryDirectoryPath = temporaryDirectory
            };

            return messages.Count == 0
                ? OperationResult<InputBatch>.Success(batch)
                : OperationResult<InputBatch>.PartialSuccess(batch, messages);
        }
        catch (OperationCanceledException)
        {
            await deleteTemporaryDirectoryAsync(temporaryDirectory).ConfigureAwait(false);
            throw;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            await deleteTemporaryDirectoryAsync(temporaryDirectory).ConfigureAwait(false);
            return failure("input.zip.unreadable", $"Unable to read ZIP archive {archivePath}: {exception.Message}");
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Normalizes ZIP separators and removes harmless leading current-directory segments.</summary>
    private static string normalizeEntryPath(string entryPath)
    {
        #region implementation

        var normalized = entryPath.Replace('\\', '/');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        return normalized;

        #endregion
    }

    /**************************************************************/
    /// <summary>Determines whether an archive entry is a non-rooted traversal-free relative path.</summary>
    private static bool isSafeRelativeEntry(string relativePath)
    {
        #region implementation

        if (string.IsNullOrWhiteSpace(relativePath)
            || relativePath.StartsWith("/", StringComparison.Ordinal)
            || Path.IsPathRooted(relativePath)
            || relativePath.Contains(':'))
        {
            return false;
        }

        return relativePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .All(segment => segment is not "." and not "..");

        #endregion
    }

    /**************************************************************/
    /// <summary>Deletes an owned temporary directory when it exists.</summary>
    private static Task deleteTemporaryDirectoryAsync(string temporaryDirectory)
    {
        #region implementation

        if (Directory.Exists(temporaryDirectory))
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }

        return Task.CompletedTask;

        #endregion
    }

    /**************************************************************/
    /// <summary>Cleans temporary content and creates a failed archive result.</summary>
    private static async Task<OperationResult<InputBatch>> failAndCleanAsync(
        string temporaryDirectory,
        string code,
        string message)
    {
        #region implementation

        await deleteTemporaryDirectoryAsync(temporaryDirectory).ConfigureAwait(false);
        return failure(code, message);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a failed ZIP-loading result.</summary>
    private static OperationResult<InputBatch> failure(string code, string message)
    {
        #region implementation

        return OperationResult<InputBatch>.Failure([error(code, message)]);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one coded error message.</summary>
    private static OperationMessage error(string code, string message)
    {
        #region implementation

        return new OperationMessage { Code = code, Message = message, Severity = OperationMessageSeverity.Error };

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one coded warning message.</summary>
    private static OperationMessage warning(string code, string message)
    {
        #region implementation

        return new OperationMessage { Code = code, Message = message, Severity = OperationMessageSeverity.Warning };

        #endregion
    }

    #endregion
}
