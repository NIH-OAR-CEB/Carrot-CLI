using Carrot.Cli.Common;

namespace Carrot.Cli.Input;

/**************************************************************/
/// <summary>
/// Selects the direct-file, folder, or ZIP loading strategy after validating the input shape.
/// </summary>
internal sealed class InputSourceResolver
{
    #region implementation

    private readonly FolderInputSourceLoader _folderLoader;
    private readonly FileInputSourceLoader _fileLoader;
    private readonly ZipInputSourceLoader _zipLoader;
    private readonly DocumentFormatCatalog _formats;

    /**************************************************************/
    /// <summary>
    /// Initializes the resolver with all supported loading strategies.
    /// </summary>
    /// <param name="folderLoader">The folder discovery strategy.</param>
    /// <param name="zipLoader">The safe ZIP extraction and discovery strategy.</param>
    /// <param name="fileLoader">The direct document-file strategy.</param>
    /// <param name="formats">The authoritative supported-format catalog.</param>
    public InputSourceResolver(
        FolderInputSourceLoader folderLoader,
        FileInputSourceLoader fileLoader,
        ZipInputSourceLoader zipLoader,
        DocumentFormatCatalog formats)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(folderLoader);
        ArgumentNullException.ThrowIfNull(fileLoader);
        ArgumentNullException.ThrowIfNull(zipLoader);
        ArgumentNullException.ThrowIfNull(formats);
        _folderLoader = folderLoader;
        _fileLoader = fileLoader;
        _zipLoader = zipLoader;
        _formats = formats;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Resolves the correct loader for a folder or ZIP path.
    /// </summary>
    /// <param name="inputPath">The input path to classify.</param>
    /// <returns>A successful loader or a structured input failure.</returns>
    internal OperationResult<IInputSourceLoader> Resolve(string inputPath)
    {
        #region implementation

        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        var fullPath = Path.GetFullPath(inputPath);

        if (Directory.Exists(fullPath))
        {
            return OperationResult<IInputSourceLoader>.Success(_folderLoader);
        }

        if (!File.Exists(fullPath))
        {
            return failure("input.path.missing", $"Path not found: {fullPath}");
        }

        if (Path.GetExtension(fullPath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult<IInputSourceLoader>.Success(_zipLoader);
        }

        if (_formats.IsSupportedDocument(fullPath))
        {
            return OperationResult<IInputSourceLoader>.Success(_fileLoader);
        }

        return failure("input.file.unsupported", $"Unsupported input file type: {Path.GetExtension(fullPath)}");

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a failed input-strategy result.</summary>
    private static OperationResult<IInputSourceLoader> failure(string code, string message)
    {
        #region implementation

        return OperationResult<IInputSourceLoader>.Failure(
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
