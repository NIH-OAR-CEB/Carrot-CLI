namespace Carrot.Cli.Reporting;

/**************************************************************/
/// <summary>
/// Defines atomic persistence of complete request and exact response JSON sidecars.
/// </summary>
/// <seealso cref="JsonArtifactWriter"/>
internal interface IJsonArtifactWriter
{
    /**************************************************************/
    /// <summary>
    /// Serializes one artifact to an atomic destination without truncating its content.
    /// </summary>
    /// <typeparam name="TArtifact">The request or response contract type.</typeparam>
    /// <param name="path">The final sidecar path.</param>
    /// <param name="artifact">The complete artifact value.</param>
    /// <param name="overwrite">Whether an existing destination may be replaced.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>A task representing serialization and atomic replacement.</returns>
    Task WriteAsync<TArtifact>(
        string path,
        TArtifact artifact,
        bool overwrite,
        CancellationToken cancellationToken);
}
