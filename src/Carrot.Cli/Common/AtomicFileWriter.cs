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
    /// <exception cref="ArgumentException">Thrown when the destination path is empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the write callback is null.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when the destination directory does not exist.</exception>
    /// <exception cref="IOException">Thrown when the destination exists without overwrite permission or promotion fails.</exception>
    internal async Task WriteAsync(
        string destinationPath,
        Func<Stream, CancellationToken, Task> writeContentAsync,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentNullException.ThrowIfNull(writeContentAsync);
        cancellationToken.ThrowIfCancellationRequested();

        var fullDestinationPath = Path.GetFullPath(destinationPath);
        var destinationDirectory = Path.GetDirectoryName(fullDestinationPath);
        if (string.IsNullOrWhiteSpace(destinationDirectory) || !Directory.Exists(destinationDirectory))
        {
            throw new DirectoryNotFoundException("The destination directory does not exist.");
        }

        if (!overwrite && File.Exists(fullDestinationPath))
        {
            throw new IOException("The destination file already exists and overwrite was not approved.");
        }

        var temporaryPath = Path.Combine(
            destinationDirectory,
            $".{Path.GetFileName(fullDestinationPath)}.{Path.GetRandomFileName()}.tmp");

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 81_920,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await writeContentAsync(stream, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

                // Force buffered bytes through the operating system before the final rename.
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, fullDestinationPath, overwrite);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // Cleanup is best effort and must not replace the original persistence failure.
                }
            }
        }

        #endregion
    }

    #endregion
}
