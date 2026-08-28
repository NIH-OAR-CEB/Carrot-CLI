namespace Carrot.Cli.Common;

/**************************************************************/
/// <summary>
/// Defines streamed SHA-256 calculation for source-file lineage without loading files into memory.
/// </summary>
internal sealed class HashService
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Computes an uppercase hexadecimal SHA-256 digest from the supplied readable stream.
    /// </summary>
    /// <param name="content">The readable source content stream.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>A task containing the 64-character digest.</returns>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    internal Task<string> ComputeSha256Async(Stream content, CancellationToken cancellationToken)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
