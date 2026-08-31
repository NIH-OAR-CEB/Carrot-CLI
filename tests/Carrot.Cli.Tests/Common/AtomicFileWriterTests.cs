using System.Text;
using Carrot.Cli.Common;
using Xunit;

namespace Carrot.Cli.Tests.Common;

/**************************************************************/
/// <summary>Verifies same-directory temporary writing, promotion, replacement, and cleanup.</summary>
public sealed class AtomicFileWriterTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Verifies a complete callback is promoted to a previously absent destination.</summary>
    [Fact]
    public async Task WriteAsync_NewDestination_PromotesCompleteContent()
    {
        #region implementation

        var root = createTemporaryDirectory();
        try
        {
            // Arrange
            var destination = Path.Combine(root, "result.bin");
            var writer = new AtomicFileWriter();

            // Act
            await writer.WriteAsync(
                destination,
                (stream, token) => stream.WriteAsync(Encoding.UTF8.GetBytes("complete"), token).AsTask(),
                overwrite: false,
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal("complete", await File.ReadAllTextAsync(destination, TestContext.Current.CancellationToken));
            assertNoTemporarySiblings(root, destination);
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies approved overwrite replaces the complete prior destination.</summary>
    [Fact]
    public async Task WriteAsync_OverwriteApproved_ReplacesExistingDestination()
    {
        #region implementation

        var root = createTemporaryDirectory();
        try
        {
            // Arrange
            var destination = Path.Combine(root, "result.bin");
            await File.WriteAllTextAsync(destination, "original", TestContext.Current.CancellationToken);
            var writer = new AtomicFileWriter();

            // Act
            await writer.WriteAsync(
                destination,
                (stream, token) => stream.WriteAsync(Encoding.UTF8.GetBytes("replacement"), token).AsTask(),
                overwrite: true,
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal("replacement", await File.ReadAllTextAsync(destination, TestContext.Current.CancellationToken));
            assertNoTemporarySiblings(root, destination);
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies rejected overwrite never invokes the callback or changes the prior file.</summary>
    [Fact]
    public async Task WriteAsync_OverwriteRejected_PreservesExistingDestination()
    {
        #region implementation

        var root = createTemporaryDirectory();
        try
        {
            // Arrange
            var destination = Path.Combine(root, "result.bin");
            await File.WriteAllTextAsync(destination, "original", TestContext.Current.CancellationToken);
            var callbackInvoked = false;
            var writer = new AtomicFileWriter();

            // Act
            var exception = await Assert.ThrowsAsync<IOException>(() => writer.WriteAsync(
                destination,
                (_, _) =>
                {
                    callbackInvoked = true;
                    return Task.CompletedTask;
                },
                overwrite: false,
                TestContext.Current.CancellationToken));

            // Assert
            Assert.Contains("overwrite", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(callbackInvoked);
            Assert.Equal("original", await File.ReadAllTextAsync(destination, TestContext.Current.CancellationToken));
            assertNoTemporarySiblings(root, destination);
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies callback failure and cancellation preserve the destination and remove temporary files.</summary>
    /// <param name="cancel">Whether the callback cancels instead of throwing an I/O failure.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WriteAsync_CallbackDoesNotComplete_PreservesDestinationAndCleansTemporaryFile(bool cancel)
    {
        #region implementation

        var root = createTemporaryDirectory();
        try
        {
            // Arrange
            var destination = Path.Combine(root, "result.bin");
            await File.WriteAllTextAsync(destination, "original", TestContext.Current.CancellationToken);
            using var cancellationSource = new CancellationTokenSource();
            var writer = new AtomicFileWriter();

            Task write(Stream stream, CancellationToken token)
            {
                if (cancel)
                {
                    cancellationSource.Cancel();
                    token.ThrowIfCancellationRequested();
                }

                throw new IOException("Controlled callback failure.");
            }

            // Act
            if (cancel)
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writer.WriteAsync(
                    destination,
                    write,
                    overwrite: true,
                    cancellationSource.Token));
            }
            else
            {
                await Assert.ThrowsAsync<IOException>(() => writer.WriteAsync(
                    destination,
                    write,
                    overwrite: true,
                    cancellationSource.Token));
            }

            // Assert
            Assert.Equal("original", await File.ReadAllTextAsync(destination, TestContext.Current.CancellationToken));
            assertNoTemporarySiblings(root, destination);
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Asserts the writer left no hidden temporary siblings for one destination.</summary>
    /// <param name="directory">The owned destination directory.</param>
    /// <param name="destination">The final destination path.</param>
    private static void assertNoTemporarySiblings(string directory, string destination)
    {
        #region implementation

        var prefix = $".{Path.GetFileName(destination)}.";
        Assert.DoesNotContain(
            Directory.EnumerateFiles(directory),
            path => Path.GetFileName(path).StartsWith(prefix, StringComparison.Ordinal));

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one isolated directory owned by an atomic-writer test.</summary>
    /// <returns>The absolute directory path.</returns>
    private static string createTemporaryDirectory()
    {
        #region implementation

        var path = Path.Combine(Path.GetTempPath(), $"carrot-atomic-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;

        #endregion
    }

    /**************************************************************/
    /// <summary>Deletes one exact atomic-writer test directory.</summary>
    /// <param name="path">The owned directory.</param>
    private static void deleteTemporaryDirectory(string path)
    {
        #region implementation

        var fullPath = Path.GetFullPath(path);
        if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, recursive: true);
        }

        #endregion
    }

    #endregion
}
