using Carrot.Cli.Common;

namespace Carrot.Cli.Reporting;

/**************************************************************/
/// <summary>
/// Defines JSON serialization followed by same-directory atomic file replacement.
/// </summary>
/// <seealso cref="IJsonArtifactWriter"/>
/// <seealso cref="AtomicFileWriter"/>
internal sealed class JsonArtifactWriter : IJsonArtifactWriter
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Initializes the artifact writer with its atomic file persistence boundary.
    /// </summary>
    /// <param name="atomicFileWriter">The same-directory temporary-write coordinator.</param>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    internal JsonArtifactWriter(AtomicFileWriter atomicFileWriter)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Serializes one complete request or response artifact through the atomic writer.
    /// </summary>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    public Task WriteAsync<TArtifact>(
        string path,
        TArtifact artifact,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
