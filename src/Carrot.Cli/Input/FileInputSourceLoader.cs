using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Microsoft.Extensions.Options;

namespace Carrot.Cli.Input;

/**************************************************************/
/// <summary>Loads one supported document file as a one-item input batch.</summary>
/// <seealso cref="IInputSourceLoader"/>
internal sealed class FileInputSourceLoader : IInputSourceLoader
{
    #region implementation

    private readonly DocumentFormatCatalog _formats;
    private readonly CarrotCliOptions _options;

    /**************************************************************/
    /// <summary>Initializes direct-file loading with shared format and size validation.</summary>
    public FileInputSourceLoader(DocumentFormatCatalog formats, IOptions<CarrotCliOptions> options)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(formats);
        ArgumentNullException.ThrowIfNull(options);
        _formats = formats;
        _options = options.Value;

        #endregion
    }

    /**************************************************************/
    /// <summary>Loads one supported file without applying folder recursion.</summary>
    public Task<OperationResult<InputBatch>> LoadAsync(
        string inputPath,
        bool recursive,
        CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var fullPath = Path.GetFullPath(inputPath);
            if (!File.Exists(fullPath))
            {
                return Task.FromResult(failure("input.file.missing", $"File not found: {fullPath}"));
            }

            if (!_formats.IsSupportedDocument(fullPath))
            {
                return Task.FromResult(failure("input.file.unsupported", $"Unsupported document type: {Path.GetExtension(fullPath)}"));
            }

            if (DocumentFormatCatalog.IsOfficeTemporaryFile(fullPath))
            {
                return Task.FromResult(failure("input.file.temporary", "Office temporary owner files cannot be prepared."));
            }

            var information = new FileInfo(fullPath);
            if (information.Length > _options.MaximumFileSizeBytes)
            {
                return Task.FromResult(failure(
                    "input.file.too-large",
                    $"{information.Name} is {information.Length:N0} bytes and exceeds the configured per-file limit."));
            }

            var containerPath = information.DirectoryName ?? Path.GetPathRoot(fullPath) ?? fullPath;
            var source = new SourceFile
            {
                SourceOrdinal = 0,
                SourceKey = fullPath,
                ContainerPath = containerPath,
                PhysicalPath = fullPath,
                RelativePath = information.Name,
                FileName = information.Name,
                Extension = information.Extension.ToLowerInvariant(),
                SizeBytes = information.Length
            };

            return Task.FromResult(OperationResult<InputBatch>.Success(new InputBatch
            {
                ContainerPath = containerPath,
                Files = Array.AsReadOnly([source])
            }));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return Task.FromResult(failure("input.file.unreadable", $"Unable to read file {inputPath}: {exception.Message}"));
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a failed direct-file result with one coded error.</summary>
    private static OperationResult<InputBatch> failure(string code, string message)
    {
        #region implementation

        return OperationResult<InputBatch>.Failure(
        [
            new OperationMessage
            {
                Code = code,
                Message = message,
                Severity = OperationMessageSeverity.Error
            }
        ]);

        #endregion
    }

    #endregion
}
