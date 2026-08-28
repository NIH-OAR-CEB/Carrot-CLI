namespace Carrot.Cli.Input;

/**************************************************************/
/// <summary>
/// Defines deterministic, reparse-point-safe document discovery from a folder.
/// </summary>
/// <seealso cref="IInputSourceLoader"/>
internal sealed class FolderInputSourceLoader : IInputSourceLoader
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Discovers supported files from a folder without following reparse points.
    /// </summary>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    public Task<InputBatch> LoadAsync(string inputPath, bool recursive, CancellationToken cancellationToken)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
