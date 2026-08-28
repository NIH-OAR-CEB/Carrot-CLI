namespace Carrot.Cli.Common;

/**************************************************************/
/// <summary>
/// Defines same-directory temporary writes followed by atomic final-destination replacement.
/// </summary>
internal sealed class AtomicFileWriter
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Writes content to a temporary sibling file and atomically promotes it on success.
    /// </summary>
    /// <param name="destinationPath">The final artifact path.</param>
    /// <param name="writeContentAsync">The callback that writes complete content to the temporary stream.</param>
    /// <param name="overwrite">Whether an existing destination may be replaced.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>A task representing the atomic write.</returns>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    internal Task WriteAsync(
        string destinationPath,
        Func<Stream, CancellationToken, Task> writeContentAsync,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
