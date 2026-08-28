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

    /**************************************************************/
    /// <summary>
    /// Safely expands and discovers supported files from a ZIP archive.
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
