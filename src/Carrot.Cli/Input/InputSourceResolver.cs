using Carrot.Cli.Common;

namespace Carrot.Cli.Input;

/**************************************************************/
/// <summary>
/// Selects the folder or ZIP loading strategy after validating the input container shape.
/// </summary>
internal sealed class InputSourceResolver
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Initializes the resolver with all supported loading strategies.
    /// </summary>
    /// <param name="folderLoader">The folder discovery strategy.</param>
    /// <param name="zipLoader">The safe ZIP extraction and discovery strategy.</param>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    internal InputSourceResolver(FolderInputSourceLoader folderLoader, ZipInputSourceLoader zipLoader)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Resolves the correct loader for a folder or ZIP path.
    /// </summary>
    /// <param name="inputPath">The input path to classify.</param>
    /// <returns>A successful loader or a structured input failure.</returns>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    internal OperationResult<IInputSourceLoader> Resolve(string inputPath)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
