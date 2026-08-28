using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Microsoft.Extensions.Options;

namespace Carrot.Cli.Input;

/**************************************************************/
/// <summary>
/// Defines deterministic, reparse-point-safe document discovery from a folder.
/// </summary>
/// <seealso cref="IInputSourceLoader"/>
internal sealed class FolderInputSourceLoader : IInputSourceLoader
{
    #region implementation

    private readonly DocumentFormatCatalog _formats;
    private readonly CarrotCliOptions _options;

    /**************************************************************/
    /// <summary>Initializes deterministic folder discovery with shared formats and safety limits.</summary>
    /// <param name="formats">The authoritative supported-format catalog.</param>
    /// <param name="options">The validated preparation safeguards.</param>
    public FolderInputSourceLoader(DocumentFormatCatalog formats, IOptions<CarrotCliOptions> options)
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
    /// Discovers supported files from a folder without following reparse points.
    /// </summary>
    public Task<OperationResult<InputBatch>> LoadAsync(
        string inputPath,
        bool recursive,
        CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(inputPath))
        {
            return Task.FromResult(failure("input.folder.missing", $"Folder not found: {inputPath}"));
        }

        try
        {
            var containerPath = Path.GetFullPath(inputPath);
            var enumerationOptions = new EnumerationOptions
            {
                RecurseSubdirectories = recursive,
                IgnoreInaccessible = false,
                ReturnSpecialDirectories = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            var candidates = Directory
                .EnumerateFiles(containerPath, "*", enumerationOptions)
                .Where(_formats.IsSupportedDocument)
                .Where(path => !DocumentFormatCatalog.IsOfficeTemporaryFile(path))
                .Select(Path.GetFullPath)
                .Select(path => new
                {
                    PhysicalPath = path,
                    RelativePath = normalizeRelativePath(Path.GetRelativePath(containerPath, path)),
                    SizeBytes = new FileInfo(path).Length
                })
                .OrderBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.RelativePath, StringComparer.Ordinal)
                .ToArray();

            var messages = new List<OperationMessage>();
            var accepted = new List<SourceFile>();
            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (candidate.SizeBytes > _options.MaximumFileSizeBytes)
                {
                    messages.Add(warning(
                        "input.file.too-large",
                        $"Skipped {candidate.RelativePath}: {candidate.SizeBytes:N0} bytes exceeds the configured per-file limit."));
                    continue;
                }

                if (accepted.Count >= _options.MaximumInputFiles)
                {
                    messages.Add(warning(
                        "input.file.limit",
                        $"Skipped remaining files after reaching the {_options.MaximumInputFiles:N0}-file limit."));
                    break;
                }

                accepted.Add(createSourceFile(
                    accepted.Count,
                    containerPath,
                    candidate.PhysicalPath,
                    candidate.RelativePath,
                    candidate.SizeBytes));
            }

            if (accepted.Count == 0)
            {
                messages.Add(error("input.folder.empty", $"No supported documents were found in {containerPath}."));
                return Task.FromResult(OperationResult<InputBatch>.Failure(messages));
            }

            var batch = new InputBatch
            {
                ContainerPath = containerPath,
                Files = accepted.AsReadOnly()
            };

            return Task.FromResult(messages.Count == 0
                ? OperationResult<InputBatch>.Success(batch)
                : OperationResult<InputBatch>.PartialSuccess(batch, messages));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return Task.FromResult(failure("input.folder.unreadable", $"Unable to read folder {inputPath}: {exception.Message}"));
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a normalized source descriptor discovered from a folder.</summary>
    private static SourceFile createSourceFile(
        int ordinal,
        string containerPath,
        string physicalPath,
        string relativePath,
        long sizeBytes)
    {
        #region implementation

        return new SourceFile
        {
            SourceOrdinal = ordinal,
            SourceKey = physicalPath,
            ContainerPath = containerPath,
            PhysicalPath = physicalPath,
            RelativePath = relativePath,
            FileName = Path.GetFileName(physicalPath),
            Extension = Path.GetExtension(physicalPath).ToLowerInvariant(),
            SizeBytes = sizeBytes
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>Normalizes a platform-relative path for deterministic display and ordering.</summary>
    private static string normalizeRelativePath(string relativePath)
    {
        #region implementation

        return relativePath.Replace(Path.DirectorySeparatorChar, '/');

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a failed folder-loading result.</summary>
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
