using System.Text.Json;
using Carrot.Cli.CarrotApi;
using Carrot.Cli.CarrotApi.Contracts;
using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Carrot.Cli.Extraction;
using Carrot.Cli.Input;
using Carrot.Cli.Processing;
using Carrot.Cli.Reporting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Carrot.Cli.Tests.Processing;

/**************************************************************/
/// <summary>
/// Verifies named request preview orchestration, structured exit mapping, and the no-cluster guarantee.
/// </summary>
public sealed class DocumentProcessingWorkflowTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Verifies preview persists the shared request payload after list validation without clustering.</summary>
    [Fact]
    public async Task PreviewAsync_ReadyDocuments_WritesSharedRequestAndNeverClusters()
    {
        #region implementation

        // Arrange
        var directory = createTemporaryDirectory();
        try
        {
            var preparation = new FakePreparationWorkflow(OperationResult<PreparedDocumentBatch>.Success(createBatch()));
            var client = new RecordingApiClient();
            var workflow = createWorkflow(preparation, client, new JsonArtifactWriter(new AtomicFileWriter()));
            var request = createRequest(outputPath: directory);

            // Act
            var result = await workflow.PreviewAsync(request, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(OperationStatus.Success, result.Status);
            Assert.Equal(ExitCodes.Success, result.ExitCode);
            Assert.Single(client.ConfigurationRequests);
            Assert.Equal(0, client.ClusterCallCount);
            var artifactPath = Assert.Single(result.ArtifactPaths);
            Assert.Equal(Path.Combine(directory, "input.request.json"), artifactPath);
            var actualRequest = JsonSerializer.Deserialize<ClusterRequest>(
                await File.ReadAllTextAsync(artifactPath, TestContext.Current.CancellationToken));
            var expectedRequest = new ClusterRequestFactory(Options.Create(new CarrotCliOptions()))
                .Create(createBatch().Documents, new ClusteringConfiguration { Algorithm = "Lingo", Language = "English" });
            Assert.NotNull(actualRequest);
            Assert.Equal(JsonSerializer.Serialize(expectedRequest), JsonSerializer.Serialize(actualRequest));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies usable documents are persisted when preparation retains file-level failures.</summary>
    [Fact]
    public async Task PreviewAsync_PartialPreparation_WritesArtifactAndReturnsPartialSuccess()
    {
        #region implementation

        // Arrange
        var preparation = new FakePreparationWorkflow(OperationResult<PreparedDocumentBatch>.PartialSuccess(
            createBatch(),
            [warning("preparation.extract", "One source could not be extracted.")]));
        var client = new RecordingApiClient();
        var writer = new RecordingJsonArtifactWriter();
        var workflow = createWorkflow(preparation, client, writer);

        // Act
        var result = await workflow.PreviewAsync(createRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OperationStatus.PartialSuccess, result.Status);
        Assert.Equal(ExitCodes.PartialSuccess, result.ExitCode);
        Assert.True(writer.WriteCalled);
        Assert.Equal(0, client.ClusterCallCount);
        Assert.Contains(result.Messages, message => message.Code == "preparation.extract");

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies no ready documents stop preview before API validation or persistence.</summary>
    [Fact]
    public async Task PreviewAsync_NoReadyDocuments_ReturnsInputFailureWithoutApiOrArtifact()
    {
        #region implementation

        // Arrange
        var preparation = new FakePreparationWorkflow(OperationResult<PreparedDocumentBatch>.Failure(
            new PreparedDocumentBatch(),
            [error("preparation.no-ready-documents", "No documents were successfully prepared.")]));
        var client = new RecordingApiClient();
        var writer = new RecordingJsonArtifactWriter();
        var workflow = createWorkflow(preparation, client, writer);

        // Act
        var result = await workflow.PreviewAsync(createRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OperationStatus.Failure, result.Status);
        Assert.Equal(ExitCodes.InputFailure, result.ExitCode);
        Assert.Empty(client.ConfigurationRequests);
        Assert.False(writer.WriteCalled);
        Assert.Equal(0, client.ClusterCallCount);

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies list failures use the endpoint exit code and cannot write or cluster.</summary>
    [Fact]
    public async Task PreviewAsync_ListFailure_ReturnsEndpointFailureWithoutArtifactOrCluster()
    {
        #region implementation

        // Arrange
        var preparation = new FakePreparationWorkflow(OperationResult<PreparedDocumentBatch>.Success(createBatch()));
        var client = new RecordingApiClient
        {
            GetConfiguration = (_, _, _, _) => Task.FromResult(OperationResult<ListResponse>.Failure(
                [error("carrot.http", "The Carrot service could not be reached.")]))
        };
        var writer = new RecordingJsonArtifactWriter();
        var workflow = createWorkflow(preparation, client, writer);

        // Act
        var result = await workflow.PreviewAsync(createRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OperationStatus.Failure, result.Status);
        Assert.Equal(ExitCodes.EndpointFailure, result.ExitCode);
        Assert.Single(client.ConfigurationRequests);
        Assert.False(writer.WriteCalled);
        Assert.Equal(0, client.ClusterCallCount);

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies artifact failures use the output exit code after successful list validation.</summary>
    [Fact]
    public async Task PreviewAsync_ArtifactFailure_ReturnsOutputFailureWithoutCluster()
    {
        #region implementation

        // Arrange
        var preparation = new FakePreparationWorkflow(OperationResult<PreparedDocumentBatch>.Success(createBatch()));
        var client = new RecordingApiClient();
        var writer = new RecordingJsonArtifactWriter { Exception = new IOException("The destination is locked.") };
        var workflow = createWorkflow(preparation, client, writer);

        // Act
        var result = await workflow.PreviewAsync(createRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OperationStatus.Failure, result.Status);
        Assert.Equal(ExitCodes.OutputFailure, result.ExitCode);
        Assert.True(writer.WriteCalled);
        Assert.Equal(0, client.ClusterCallCount);
        Assert.Contains(result.Messages, message => message.Code == "preview.artifact.write");

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies caller cancellation returns the documented cancellation result without side effects.</summary>
    [Fact]
    public async Task PreviewAsync_CancelledBeforePreparation_ReturnsCancellation()
    {
        #region implementation

        // Arrange
        var preparation = new FakePreparationWorkflow(OperationResult<PreparedDocumentBatch>.Success(createBatch()));
        var client = new RecordingApiClient();
        var writer = new RecordingJsonArtifactWriter();
        var workflow = createWorkflow(preparation, client, writer);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        // Act
        var result = await workflow.PreviewAsync(createRequest(), cancellationSource.Token);

        // Assert
        Assert.Equal(OperationStatus.Failure, result.Status);
        Assert.Equal(ExitCodes.Cancellation, result.ExitCode);
        Assert.Empty(client.ConfigurationRequests);
        Assert.False(writer.WriteCalled);
        Assert.Equal(0, client.ClusterCallCount);

        #endregion
    }

    /**************************************************************/
    /// <summary>Reserves complete clustering and reporting acceptance for the later named-process milestone.</summary>
    [Fact(Skip = "Future acceptance: named document processing orchestration is layout-only.")]
    public void ProcessPreservesGlobalClusteringSemanticsAndSourceCorrelation()
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a workflow with production configuration behavior and controlled volatile collaborators.</summary>
    private static DocumentProcessingWorkflow createWorkflow(
        IDocumentPreparationWorkflow preparation,
        RecordingApiClient client,
        IJsonArtifactWriter writer)
    {
        #region implementation

        var options = Options.Create(new CarrotCliOptions());
        return new DocumentProcessingWorkflow(
            preparation,
            new ClusteringConfigurationResolver(options),
            new ClusteringConfigurationValidator(),
            client,
            new ClusterRequestFactory(options),
            writer);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a direct-selection preview request with a deterministic virtual input path.</summary>
    private static PreviewRequest createRequest(string? outputPath = null)
    {
        #region implementation

        return new PreviewRequest
        {
            InputPath = Path.Combine(Path.GetTempPath(), "input.txt"),
            Endpoint = new Uri("https://carrot.example/service"),
            OutputPath = outputPath,
            Clustering = new ClusteringSelection { Algorithm = "Lingo", Language = "English" },
            Timeout = TimeSpan.FromSeconds(30)
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one isolated output directory for an actual atomic preview artifact.</summary>
    private static string createTemporaryDirectory()
    {
        #region implementation

        var directory = Path.Combine(Path.GetTempPath(), $"carrot-preview-workflow-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a prepared batch containing the exact documents that must appear in the request.</summary>
    private static PreparedDocumentBatch createBatch()
    {
        #region implementation

        return new PreparedDocumentBatch
        {
            Documents =
            [
                createDocument(0, "first.txt", "First title", "First content"),
                createDocument(1, "second.txt", "Second title", "Second content")
            ]
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one extracted document with source-only metadata excluded from the request payload.</summary>
    private static ExtractedDocument createDocument(int index, string fileName, string title, string content)
    {
        #region implementation

        return new ExtractedDocument
        {
            SourceFile = new SourceFile
            {
                SourceOrdinal = index,
                SourceKey = fileName,
                ContainerPath = Path.GetTempPath(),
                PhysicalPath = Path.Combine(Path.GetTempPath(), fileName),
                RelativePath = fileName,
                FileName = fileName,
                Extension = ".txt"
            },
            CarrotDocumentIndex = index,
            Title = title,
            Content = content,
            Sha256 = "ABCDEF"
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates the controlled list response advertising the test direct selection.</summary>
    private static ListResponse createListResponse()
    {
        #region implementation

        return new ListResponse
        {
            Algorithms = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["Lingo"] = ["English"]
            },
            Templates = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one safe error operation message for controlled dependency outcomes.</summary>
    private static OperationMessage error(string code, string message)
    {
        #region implementation

        return new OperationMessage { Code = code, Message = message, Severity = OperationMessageSeverity.Error };

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one safe warning operation message for usable partial preparation outcomes.</summary>
    private static OperationMessage warning(string code, string message)
    {
        #region implementation

        return new OperationMessage { Code = code, Message = message, Severity = OperationMessageSeverity.Warning };

        #endregion
    }

    /**************************************************************/
    /// <summary>Returns a configured preparation outcome without accessing the file system.</summary>
    private sealed class FakePreparationWorkflow : IDocumentPreparationWorkflow
    {
        #region implementation

        private readonly OperationResult<PreparedDocumentBatch> _result;

        /**************************************************************/
        /// <summary>Initializes the fake with the exact preparation outcome to return.</summary>
        internal FakePreparationWorkflow(OperationResult<PreparedDocumentBatch> result)
        {
            #region implementation

            _result = result;

            #endregion
        }

        /**************************************************************/
        /// <summary>Returns the configured preparation result after honoring immediate cancellation.</summary>
        public Task<OperationResult<PreparedDocumentBatch>> PrepareAsync(
            PrepareDocumentsRequest request,
            CancellationToken cancellationToken)
        {
            #region implementation

            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_result);

            #endregion
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Records list calls and rejects every cluster call made by a preview workflow.</summary>
    private sealed class RecordingApiClient : ICarrotApiClient
    {
        #region implementation

        /**************************************************************/
        /// <summary>Gets or sets behavior for the list endpoint.</summary>
        internal Func<Uri, TimeSpan, bool?, CancellationToken, Task<OperationResult<ListResponse>>> GetConfiguration { get; set; }
            = (_, _, _, _) => Task.FromResult(OperationResult<ListResponse>.Success(createListResponse()));

        /**************************************************************/
        /// <summary>Gets recorded list requests.</summary>
        internal List<(Uri Endpoint, TimeSpan Timeout, bool? Indent)> ConfigurationRequests { get; } = [];

        /**************************************************************/
        /// <summary>Gets the number of prohibited cluster submissions.</summary>
        internal int ClusterCallCount { get; private set; }

        /**************************************************************/
        /// <summary>Records and returns the controlled list result.</summary>
        public Task<OperationResult<ListResponse>> GetConfigurationAsync(
            Uri serviceEndpoint,
            TimeSpan timeout,
            bool? indent,
            CancellationToken cancellationToken)
        {
            #region implementation

            ConfigurationRequests.Add((serviceEndpoint, timeout, indent));
            return GetConfiguration(serviceEndpoint, timeout, indent, cancellationToken);

            #endregion
        }

        /**************************************************************/
        /// <summary>Records the prohibited cluster call so tests can enforce the preview contract.</summary>
        public Task<OperationResult<ClusterResponse>> ClusterAsync(
            Uri serviceEndpoint,
            ClusterRequest request,
            string? template,
            TimeSpan timeout,
            bool? indent,
            CancellationToken cancellationToken)
        {
            #region implementation

            ClusterCallCount++;
            throw new InvalidOperationException("Preview must not call the cluster endpoint.");

            #endregion
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Records the exact generic artifact supplied to atomic request persistence.</summary>
    private sealed class RecordingJsonArtifactWriter : IJsonArtifactWriter
    {
        #region implementation

        /**************************************************************/
        /// <summary>Gets or sets the controlled persistence exception.</summary>
        internal Exception? Exception { get; init; }

        /**************************************************************/
        /// <summary>Gets whether persistence was invoked.</summary>
        internal bool WriteCalled { get; private set; }

        /**************************************************************/
        /// <summary>Gets the requested output path.</summary>
        internal string? Path { get; private set; }

        /**************************************************************/
        /// <summary>Gets the exact request artifact passed to the writer.</summary>
        internal object? Artifact { get; private set; }

        /**************************************************************/
        /// <summary>Records a write call or returns the configured persistence failure.</summary>
        public Task WriteAsync<TArtifact>(
            string path,
            TArtifact artifact,
            bool overwrite,
            CancellationToken cancellationToken)
        {
            #region implementation

            WriteCalled = true;
            Path = path;
            Artifact = artifact;
            return Exception is null ? Task.CompletedTask : Task.FromException(Exception);

            #endregion
        }

        #endregion
    }

    #endregion
}
