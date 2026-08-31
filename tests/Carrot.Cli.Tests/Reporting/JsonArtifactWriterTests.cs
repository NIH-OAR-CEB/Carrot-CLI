using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Carrot.Cli.CarrotApi.Contracts;
using Carrot.Cli.Common;
using Carrot.Cli.Reporting;
using Xunit;

namespace Carrot.Cli.Tests.Reporting;

/**************************************************************/
/// <summary>
/// Verifies complete UTF-8 JSON serialization, round trips, overwrite rules, cancellation, and cleanup.
/// </summary>
public sealed class JsonArtifactWriterTests
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Verifies a complete request is written as indented UTF-8 and preserves every JSON value kind.
    /// </summary>
    [Fact]
    public async Task WriteAsync_ClusterRequest_WritesCompleteIndentedUtf8Artifact()
    {
        #region implementation

        var root = createTemporaryDirectory();
        try
        {
            // Arrange
            var destination = Path.Combine(root, "request.json");
            var request = new ClusterRequest
            {
                Language = "English",
                Algorithm = "Lingo",
                Parameters = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                {
                    ["number"] = element("17.25"),
                    ["enabled"] = element("true"),
                    ["name"] = element("\"value\""),
                    ["items"] = element("[1, \"two\", false, null]"),
                    ["nested"] = element("{\"child\": {\"count\": 3}}"),
                    ["nothing"] = element("null")
                },
                Documents =
                [
                    new ClusterDocument
                    {
                        Title = "=SUM(A1:A2)",
                        Content = "Complete extracted text Ω",
                        AdditionalFields = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                        {
                            ["tags"] = element("[\"one\", \"two\"]")
                        }
                    }
                ]
            };
            var writer = new JsonArtifactWriter(new AtomicFileWriter());

            // Act
            await writer.WriteAsync(
                destination,
                request,
                overwrite: false,
                TestContext.Current.CancellationToken);

            // Assert
            var bytes = await File.ReadAllBytesAsync(destination, TestContext.Current.CancellationToken);
            Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
            var json = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(bytes);
            Assert.StartsWith("{", json, StringComparison.Ordinal);
            Assert.Matches("\\r?\\n  \\\"language\\\":", json);

            var roundTrip = Assert.IsType<ClusterRequest>(JsonSerializer.Deserialize<ClusterRequest>(json));
            Assert.Equal("=SUM(A1:A2)", Assert.Single(roundTrip.Documents).Title);
            Assert.Equal("Complete extracted text Ω", roundTrip.Documents[0].Content);
            Assert.Equal(JsonValueKind.Number, roundTrip.Parameters!["number"].ValueKind);
            Assert.Equal(JsonValueKind.True, roundTrip.Parameters["enabled"].ValueKind);
            Assert.Equal(JsonValueKind.String, roundTrip.Parameters["name"].ValueKind);
            Assert.Equal(JsonValueKind.Array, roundTrip.Parameters["items"].ValueKind);
            Assert.Equal(JsonValueKind.Object, roundTrip.Parameters["nested"].ValueKind);
            Assert.Equal(JsonValueKind.Null, roundTrip.Parameters["nothing"].ValueKind);

            using var persistedDocument = JsonDocument.Parse(json);
            var expected = JsonSerializer.SerializeToElement(request);
            Assert.True(JsonElement.DeepEquals(expected, persistedDocument.RootElement));
            assertNoTemporarySiblings(root, destination);
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies recursive response labels, indexes, scores, and children survive file persistence.
    /// </summary>
    [Fact]
    public async Task WriteAsync_ClusterResponse_RoundTripsRecursiveContract()
    {
        #region implementation

        var root = createTemporaryDirectory();
        try
        {
            // Arrange
            var destination = Path.Combine(root, "response.json");
            var response = new ClusterResponse
            {
                Clusters =
                [
                    new ClusterNode
                    {
                        Labels = ["[literal]", "=FORMULA()"],
                        Documents = [0, 2],
                        Score = 12.375D,
                        Clusters =
                        [
                            new ClusterNode
                            {
                                Labels = ["Nested"],
                                Documents = [2],
                                Score = 0.125D
                            }
                        ]
                    }
                ]
            };
            var writer = new JsonArtifactWriter(new AtomicFileWriter());

            // Act
            await writer.WriteAsync(
                destination,
                response,
                overwrite: false,
                TestContext.Current.CancellationToken);

            // Assert
            var json = await File.ReadAllTextAsync(destination, TestContext.Current.CancellationToken);
            var roundTrip = Assert.IsType<ClusterResponse>(JsonSerializer.Deserialize<ClusterResponse>(json));
            var cluster = Assert.Single(roundTrip.Clusters);
            Assert.Equal(new[] { "[literal]", "=FORMULA()" }, cluster.Labels);
            Assert.Equal(new[] { 0, 2 }, cluster.Documents);
            Assert.Equal(12.375D, cluster.Score);
            Assert.Equal("Nested", Assert.Single(cluster.Clusters).Labels[0]);
            Assert.Equal(0.125D, cluster.Clusters[0].Score);
            assertNoTemporarySiblings(root, destination);
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies an existing JSON file is unchanged when overwrite permission is absent.
    /// </summary>
    [Fact]
    public async Task WriteAsync_ExistingDestinationWithoutOverwrite_PreservesOriginal()
    {
        #region implementation

        var root = createTemporaryDirectory();
        try
        {
            // Arrange
            var destination = Path.Combine(root, "artifact.json");
            await File.WriteAllTextAsync(destination, "original", TestContext.Current.CancellationToken);
            var writer = new JsonArtifactWriter(new AtomicFileWriter());

            // Act
            var exception = await Assert.ThrowsAsync<IOException>(() => writer.WriteAsync(
                destination,
                new { value = "replacement" },
                overwrite: false,
                TestContext.Current.CancellationToken));

            // Assert
            Assert.Contains("overwrite", exception.Message, StringComparison.OrdinalIgnoreCase);
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
    /// <summary>
    /// Verifies explicit overwrite permission atomically replaces an existing JSON destination.
    /// </summary>
    [Fact]
    public async Task WriteAsync_ExistingDestinationWithOverwrite_ReplacesOriginal()
    {
        #region implementation

        var root = createTemporaryDirectory();
        try
        {
            // Arrange
            var destination = Path.Combine(root, "artifact.json");
            await File.WriteAllTextAsync(destination, "original", TestContext.Current.CancellationToken);
            var writer = new JsonArtifactWriter(new AtomicFileWriter());

            // Act
            await writer.WriteAsync(
                destination,
                new { value = "replacement" },
                overwrite: true,
                TestContext.Current.CancellationToken);

            // Assert
            using var document = JsonDocument.Parse(
                await File.ReadAllTextAsync(destination, TestContext.Current.CancellationToken));
            Assert.Equal("replacement", document.RootElement.GetProperty("value").GetString());
            assertNoTemporarySiblings(root, destination);
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies cancellation during asynchronous serialization preserves the destination and removes partial output.
    /// </summary>
    [Fact]
    public async Task WriteAsync_CancelledDuringSerialization_PreservesOriginalAndCleansTemporaryFile()
    {
        #region implementation

        var root = createTemporaryDirectory();
        try
        {
            // Arrange
            var destination = Path.Combine(root, "artifact.json");
            await File.WriteAllTextAsync(destination, "original", TestContext.Current.CancellationToken);
            using var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken);
            var writer = new JsonArtifactWriter(new AtomicFileWriter());
            var artifact = cancelDuringEnumeration(
                cancellationSource,
                TestContext.Current.CancellationToken);

            // Act
#pragma warning disable xUnit1051 // This test must control cancellation after the temporary stream has content.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writer.WriteAsync(
                destination,
                artifact,
                overwrite: true,
                cancellationSource.Token));
#pragma warning restore xUnit1051

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
    /// <summary>
    /// Verifies serialization failure exposes no destination and removes the incomplete temporary sibling.
    /// </summary>
    [Fact]
    public async Task WriteAsync_CyclicArtifact_LeavesNoPartialFile()
    {
        #region implementation

        var root = createTemporaryDirectory();
        try
        {
            // Arrange
            var destination = Path.Combine(root, "artifact.json");
            var artifact = new CyclicArtifact();
            artifact.Self = artifact;
            var writer = new JsonArtifactWriter(new AtomicFileWriter());

            // Act
            await Assert.ThrowsAsync<JsonException>(() => writer.WriteAsync(
                destination,
                artifact,
                overwrite: false,
                TestContext.Current.CancellationToken));

            // Assert
            Assert.False(File.Exists(destination));
            assertNoTemporarySiblings(root, destination);
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies a missing destination directory propagates safely without creating an artifact.
    /// </summary>
    [Fact]
    public async Task WriteAsync_MissingDestinationDirectory_PropagatesFailure()
    {
        #region implementation

        var root = createTemporaryDirectory();
        try
        {
            // Arrange
            var destination = Path.Combine(root, "missing", "artifact.json");
            var writer = new JsonArtifactWriter(new AtomicFileWriter());

            // Act and assert
            await Assert.ThrowsAsync<DirectoryNotFoundException>(() => writer.WriteAsync(
                destination,
                new { value = 1 },
                overwrite: false,
                TestContext.Current.CancellationToken));
            Assert.False(File.Exists(destination));
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies the public generic method rejects empty paths and null reference artifacts synchronously.
    /// </summary>
    [Fact]
    public void WriteAsync_InvalidArguments_ThrowArgumentExceptions()
    {
        #region implementation

        // Arrange
        var writer = new JsonArtifactWriter(new AtomicFileWriter());

        // Act and assert
        Assert.Throws<ArgumentException>(() =>
        {
            _ = writer.WriteAsync(
                "   ",
                new { value = 1 },
                overwrite: false,
                TestContext.Current.CancellationToken);
        });
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = writer.WriteAsync<object>(
                "artifact.json",
                null!,
                overwrite: false,
                TestContext.Current.CancellationToken);
        });

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies construction rejects a missing atomic persistence dependency.
    /// </summary>
    [Fact]
    public void Constructor_NullAtomicWriter_ThrowsArgumentNullException()
    {
        #region implementation

        // Act and assert
        Assert.Throws<ArgumentNullException>(() => new JsonArtifactWriter(null!));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Produces one value before cancelling the token used by asynchronous JSON serialization.
    /// </summary>
    /// <param name="cancellationSource">The source whose token is supplied to the artifact writer.</param>
    /// <param name="cancellationToken">The serializer-provided asynchronous enumeration token.</param>
    /// <returns>An asynchronous sequence that cancels before completing.</returns>
    private static async IAsyncEnumerable<int> cancelDuringEnumeration(
        CancellationTokenSource cancellationSource,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        #region implementation

        yield return 1;
        await Task.Yield();
        cancellationSource.Cancel();
        cancellationToken.ThrowIfCancellationRequested();

        #endregion
    }

    /**************************************************************/
    /// <summary>Parses and detaches one JSON value for request-contract fixtures.</summary>
    /// <param name="json">The complete scalar, array, object, or null JSON value.</param>
    /// <returns>A detached JSON element safe after the parser is disposed.</returns>
    private static JsonElement element(string json)
    {
        #region implementation

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();

        #endregion
    }

    /**************************************************************/
    /// <summary>Asserts that one destination has no hidden atomic-writer temporary siblings.</summary>
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
    /// <summary>Creates one isolated filesystem directory for JSON artifact tests.</summary>
    /// <returns>The absolute owned directory path.</returns>
    private static string createTemporaryDirectory()
    {
        #region implementation

        var path = Path.Combine(Path.GetTempPath(), $"carrot-json-artifact-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;

        #endregion
    }

    /**************************************************************/
    /// <summary>Deletes one exact JSON artifact test directory.</summary>
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

    /**************************************************************/
    /// <summary>
    /// Defines an intentionally cyclic graph used to prove serialization-failure cleanup.
    /// </summary>
    private sealed class CyclicArtifact
    {
        #region implementation

        /**************************************************************/
        /// <summary>Gets or sets the self-reference that makes this graph cyclic.</summary>
        public CyclicArtifact? Self { get; set; }

        #endregion
    }

    #endregion
}
