using System.Text.Json;
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

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly AtomicFileWriter _atomicFileWriter;

    /**************************************************************/
    /// <summary>
    /// Initializes the artifact writer with its atomic file persistence boundary.
    /// </summary>
    /// <param name="atomicFileWriter">The same-directory temporary-write coordinator.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="atomicFileWriter"/> is null.
    /// </exception>
    /// <seealso cref="AtomicFileWriter"/>
    public JsonArtifactWriter(AtomicFileWriter atomicFileWriter)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(atomicFileWriter);
        _atomicFileWriter = atomicFileWriter;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Serializes one complete request or response artifact through the atomic writer.
    /// </summary>
    /// <remarks>
    /// <see cref="JsonSerializer.SerializeAsync{TValue}(Stream, TValue, JsonSerializerOptions?, CancellationToken)"/>
    /// emits indented UTF-8 JSON directly to the atomic writer's same-directory temporary stream.
    /// Expected filesystem and serialization exceptions remain available to the calling workflow,
    /// which owns conversion to a documented artifact exit code.
    /// </remarks>
    /// <typeparam name="TArtifact">The request, response, or compatible JSON artifact type.</typeparam>
    /// <param name="path">The final artifact path.</param>
    /// <param name="artifact">The complete non-null artifact value.</param>
    /// <param name="overwrite">Whether an existing destination may be atomically replaced.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>A task representing serialization, durable flush, and atomic promotion.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="artifact"/> is null.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when the destination directory does not exist.</exception>
    /// <exception cref="IOException">
    /// Thrown when an existing destination cannot be overwritten or persistence fails.
    /// </exception>
    /// <exception cref="JsonException">Thrown when the artifact graph cannot be serialized as valid JSON.</exception>
    /// <exception cref="NotSupportedException">Thrown when the artifact contains an unsupported type.</exception>
    /// <exception cref="OperationCanceledException">Thrown when cancellation is requested.</exception>
    /// <seealso cref="AtomicFileWriter.WriteAsync"/>
    public Task WriteAsync<TArtifact>(
        string path,
        TArtifact artifact,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(artifact);

        return _atomicFileWriter.WriteAsync(
            path,
            (stream, token) => JsonSerializer.SerializeAsync(
                stream,
                artifact,
                SerializerOptions,
                token),
            overwrite,
            cancellationToken);

        #endregion
    }

    #endregion
}
