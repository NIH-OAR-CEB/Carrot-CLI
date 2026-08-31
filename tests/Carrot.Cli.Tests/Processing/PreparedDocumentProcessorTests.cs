using System.Text.Json;
using Carrot.Cli.CarrotApi;
using Carrot.Cli.CarrotApi.Contracts;
using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Carrot.Cli.Extraction;
using Carrot.Cli.Input;
using Carrot.Cli.Processing;
using Microsoft.Extensions.Options;
using Xunit;

namespace Carrot.Cli.Tests.Processing;

/**************************************************************/
/// <summary>Verifies list-first processing, exact request reuse, correlation, validation, and failures.</summary>
public sealed class PreparedDocumentProcessorTests
{
    #region implementation

    private static readonly Uri Endpoint = new("http://localhost:8080/service");
    private static readonly Guid ExpectedRunId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    /**************************************************************/
    /// <summary>Verifies list precedes cluster and response indexes address prepared order rather than source ordinals.</summary>
    [Fact]
    public async Task ProcessAsync_PreparedBatchWithSourceGaps_CorrelatesExactSubmittedArray()
    {
        #region implementation

        // Arrange
        var apiClient = new StubCarrotApiClient
        {
            ConfigurationResult = successfulConfiguration(),
            ClusterResult = OperationResult<ClusterResponse>.Success(new ClusterResponse
            {
                Clusters =
                [
                    new ClusterNode
                    {
                        Labels = ["Second submitted"],
                        Documents = [1],
                        Score = 7.75D
                    },
                    new ClusterNode
                    {
                        Labels = ["Both"],
                        Documents = [0, 1],
                        Score = 3.125D
                    }
                ]
            })
        };
        var requestFactory = new ClusterRequestFactory(Options.Create(new CarrotCliOptions()));
        var processor = new PreparedDocumentProcessor(
            requestFactory,
            apiClient,
            new ClusterMembershipMapper(),
            new StubRunIdProvider(ExpectedRunId));
        var preparedBatch = createBatchWithFailedSourceGaps();

        // Act
        var result = await processor.ProcessAsync(
            new ProcessPreparedItemsRequest
            {
                Endpoint = Endpoint,
                PreparedBatch = preparedBatch,
                Timeout = TimeSpan.FromSeconds(120)
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(["list", "cluster"], apiClient.Calls);
        Assert.NotNull(apiClient.SubmittedRequest);
        Assert.Same(apiClient.SubmittedRequest, result.Value!.Request);
        Assert.Equal(ExpectedRunId, result.Value.RunId);
        Assert.Equal(["ready-one", "ready-three"], result.Value.Request.Documents.Select(document => document.Title));
        Assert.Collection(
            result.Value.Rows,
            row =>
            {
                Assert.Equal(0, row.CarrotDocumentIndex);
                Assert.Equal(1, row.PreparedDocument.SourceFile.SourceOrdinal);
                Assert.Equal(["Both"], row.Memberships.Select(membership => membership.CategoryPath));
            },
            row =>
            {
                Assert.Equal(1, row.CarrotDocumentIndex);
                Assert.Equal(3, row.PreparedDocument.SourceFile.SourceOrdinal);
                Assert.Equal(
                    ["Second submitted", "Both"],
                    row.Memberships.Select(membership => membership.CategoryPath));
            });
        Assert.Equal(2, result.Value.AssignedCount);
        Assert.Equal(0, result.Value.UnassignedCount);
        Assert.Equal(3, result.Value.MembershipCount);

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies an empty successful cluster response produces one explicit unassigned row per ready document.</summary>
    [Fact]
    public async Task ProcessAsync_EmptyClusters_ReturnsEveryDocumentAsUnassigned()
    {
        #region implementation

        // Arrange
        var apiClient = new StubCarrotApiClient
        {
            ConfigurationResult = successfulConfiguration(),
            ClusterResult = OperationResult<ClusterResponse>.Success(new ClusterResponse())
        };
        var processor = createProcessor(apiClient);

        // Act
        var result = await processor.ProcessAsync(
            createRequest(createBatchWithFailedSourceGaps()),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(2, result.Value!.Rows.Count);
        Assert.All(result.Value.Rows, row =>
        {
            Assert.False(row.IsAssigned);
            Assert.Empty(row.Memberships);
        });
        Assert.Equal(2, result.Value.UnassignedCount);

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies exact algorithm and language identifiers must both appear in the list response.</summary>
    /// <param name="algorithmsJson">The algorithm map represented as JSON.</param>
    /// <param name="expectedCode">The expected validation failure code.</param>
    [Theory]
    [InlineData("{ \"lingo\": [ \"English\" ] }", "processing.algorithm-unavailable")]
    [InlineData("{ \"Lingo\": [ \"english\" ] }", "processing.language-unavailable")]
    public async Task ProcessAsync_ConfigurationIdentifierMismatch_DoesNotSubmitCluster(
        string algorithmsJson,
        string expectedCode)
    {
        #region implementation

        // Arrange
        var algorithms = JsonSerializer.Deserialize<Dictionary<string, IReadOnlyList<string>>>(algorithmsJson)!;
        var apiClient = new StubCarrotApiClient
        {
            ConfigurationResult = OperationResult<ListResponse>.Success(new ListResponse
            {
                Algorithms = algorithms,
                Templates = new Dictionary<string, JsonElement>()
            }),
            ClusterResult = OperationResult<ClusterResponse>.Success(new ClusterResponse())
        };
        var processor = createProcessor(apiClient);

        // Act
        var result = await processor.ProcessAsync(
            createRequest(createBatchWithFailedSourceGaps()),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OperationStatus.Failure, result.Status);
        Assert.Equal(expectedCode, Assert.Single(result.Messages).Code);
        Assert.Equal(["list"], apiClient.Calls);
        Assert.Null(apiClient.SubmittedRequest);

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies an invalid response index rejects the complete processed batch.</summary>
    [Fact]
    public async Task ProcessAsync_InvalidResponseIndex_ReturnsNoPartialBatch()
    {
        #region implementation

        // Arrange
        var apiClient = new StubCarrotApiClient
        {
            ConfigurationResult = successfulConfiguration(),
            ClusterResult = OperationResult<ClusterResponse>.Success(new ClusterResponse
            {
                Clusters = [new ClusterNode { Labels = ["Invalid"], Documents = [2] }]
            })
        };
        var processor = createProcessor(apiClient);

        // Act
        var result = await processor.ProcessAsync(
            createRequest(createBatchWithFailedSourceGaps()),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OperationStatus.Failure, result.Status);
        Assert.Null(result.Value);
        Assert.Equal("carrot.response.document-index", Assert.Single(result.Messages).Code);

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies a retained batch with zero ready items returns before any HTTP operation.</summary>
    [Fact]
    public async Task ProcessAsync_ZeroReadyItems_ReturnsFailureWithoutApiCall()
    {
        #region implementation

        // Arrange
        var apiClient = new StubCarrotApiClient();
        var processor = createProcessor(apiClient);

        // Act
        var result = await processor.ProcessAsync(
            createRequest(new PreparedDocumentBatch()),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OperationStatus.Failure, result.Status);
        Assert.Equal("processing.no-ready-documents", Assert.Single(result.Messages).Code);
        Assert.Empty(apiClient.Calls);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates the processor with default Lingo/English request configuration.</summary>
    /// <param name="apiClient">The controlled API boundary.</param>
    /// <returns>The processor under test.</returns>
    private static PreparedDocumentProcessor createProcessor(ICarrotApiClient apiClient)
    {
        #region implementation

        return new PreparedDocumentProcessor(
            new ClusterRequestFactory(Options.Create(new CarrotCliOptions())),
            apiClient,
            new ClusterMembershipMapper(),
            new StubRunIdProvider(ExpectedRunId));

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a processing request for one prepared batch.</summary>
    /// <param name="batch">The prepared batch.</param>
    /// <returns>The request fixture.</returns>
    private static ProcessPreparedItemsRequest createRequest(PreparedDocumentBatch batch)
    {
        #region implementation

        return new ProcessPreparedItemsRequest
        {
            Endpoint = Endpoint,
            PreparedBatch = batch,
            Timeout = TimeSpan.FromSeconds(120)
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates exact Lingo/English list configuration.</summary>
    /// <returns>A successful list operation.</returns>
    private static OperationResult<ListResponse> successfulConfiguration()
    {
        #region implementation

        return OperationResult<ListResponse>.Success(new ListResponse
        {
            Algorithms = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["Lingo"] = ["English"]
            },
            Templates = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        });

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates two ready documents whose source ordinals are separated by failed rows.</summary>
    /// <returns>The mixed prepared batch.</returns>
    private static PreparedDocumentBatch createBatchWithFailedSourceGaps()
    {
        #region implementation

        var first = createDocument(sourceOrdinal: 1, carrotIndex: 0, "ready-one");
        var second = createDocument(sourceOrdinal: 3, carrotIndex: 1, "ready-three");
        return new PreparedDocumentBatch
        {
            Documents = [first, second],
            Rows =
            [
                failedRow(0, "failed-zero.txt"),
                readyRow(first),
                failedRow(2, "failed-two.txt"),
                readyRow(second)
            ]
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one extracted-document fixture.</summary>
    /// <param name="sourceOrdinal">The noncontiguous global source ordinal.</param>
    /// <param name="carrotIndex">The contiguous request index.</param>
    /// <param name="title">The request title.</param>
    /// <returns>The extracted document.</returns>
    private static ExtractedDocument createDocument(int sourceOrdinal, int carrotIndex, string title)
    {
        #region implementation

        var fileName = $"{title}.txt";
        var path = $"C:\\Inputs\\{fileName}";
        return new ExtractedDocument
        {
            SourceFile = new SourceFile
            {
                SourceOrdinal = sourceOrdinal,
                SourceKey = path,
                ContainerPath = "C:\\Inputs",
                PhysicalPath = path,
                RelativePath = fileName,
                FileName = fileName,
                Extension = ".txt",
                SizeBytes = 20
            },
            CarrotDocumentIndex = carrotIndex,
            Title = title,
            Content = $"Complete content for {title}",
            Sha256 = new string('A', 64)
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one ready review row paired with an extracted document.</summary>
    /// <param name="document">The paired ready document.</param>
    /// <returns>The ready row.</returns>
    private static PreparedDocumentRow readyRow(ExtractedDocument document)
    {
        #region implementation

        return new PreparedDocumentRow
        {
            SourceOrdinal = document.SourceFile.SourceOrdinal,
            CarrotDocumentIndex = document.CarrotDocumentIndex,
            ContainerPath = document.SourceFile.ContainerPath,
            RelativePath = document.SourceFile.RelativePath,
            FileName = document.SourceFile.FileName,
            Extension = document.SourceFile.Extension,
            SizeBytes = document.SourceFile.SizeBytes,
            Status = PreparedDocumentStatus.Ready
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one failed review row that introduces a source-ordinal gap.</summary>
    /// <param name="sourceOrdinal">The failed source ordinal.</param>
    /// <param name="fileName">The failed filename.</param>
    /// <returns>The failed row.</returns>
    private static PreparedDocumentRow failedRow(int sourceOrdinal, string fileName)
    {
        #region implementation

        return new PreparedDocumentRow
        {
            SourceOrdinal = sourceOrdinal,
            ContainerPath = "C:\\Inputs",
            RelativePath = fileName,
            FileName = fileName,
            Extension = ".txt",
            Status = PreparedDocumentStatus.Failed,
            ErrorMessage = "Expected fixture failure."
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>Captures API ordering and returns configured list and cluster outcomes.</summary>
    private sealed class StubCarrotApiClient : ICarrotApiClient
    {
        #region implementation

        /**************************************************************/
        /// <summary>Gets or sets the list operation result.</summary>
        internal OperationResult<ListResponse> ConfigurationResult { get; set; }
            = OperationResult<ListResponse>.Failure(
                [new OperationMessage { Code = "test.list", Message = "List result not configured.", Severity = OperationMessageSeverity.Error }]);

        /**************************************************************/
        /// <summary>Gets or sets the cluster operation result.</summary>
        internal OperationResult<ClusterResponse> ClusterResult { get; set; }
            = OperationResult<ClusterResponse>.Failure(
                [new OperationMessage { Code = "test.cluster", Message = "Cluster result not configured.", Severity = OperationMessageSeverity.Error }]);

        /**************************************************************/
        /// <summary>Gets API calls in invocation order.</summary>
        internal List<string> Calls { get; } = [];

        /**************************************************************/
        /// <summary>Gets the exact submitted request instance.</summary>
        internal ClusterRequest? SubmittedRequest { get; private set; }

        /**************************************************************/
        /// <summary>Returns the configured list response and records ordering.</summary>
        public Task<OperationResult<ListResponse>> GetConfigurationAsync(
            Uri serviceEndpoint,
            TimeSpan timeout,
            bool? indent,
            CancellationToken cancellationToken)
        {
            #region implementation

            Calls.Add("list");
            Assert.Null(indent);
            return Task.FromResult(ConfigurationResult);

            #endregion
        }

        /**************************************************************/
        /// <summary>Captures the exact request and returns the configured cluster response.</summary>
        public Task<OperationResult<ClusterResponse>> ClusterAsync(
            Uri serviceEndpoint,
            ClusterRequest request,
            string? template,
            TimeSpan timeout,
            bool? indent,
            CancellationToken cancellationToken)
        {
            #region implementation

            Calls.Add("cluster");
            SubmittedRequest = request;
            Assert.Null(template);
            Assert.Null(indent);
            return Task.FromResult(ClusterResult);

            #endregion
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Returns one deterministic successful-run correlation identifier.</summary>
    private sealed class StubRunIdProvider : IRunIdProvider
    {
        #region implementation

        private readonly Guid _runId;

        /**************************************************************/
        /// <summary>Initializes the provider with the identifier returned by every request.</summary>
        /// <param name="runId">The deterministic run identifier.</param>
        internal StubRunIdProvider(Guid runId)
        {
            #region implementation

            _runId = runId;

            #endregion
        }

        /**************************************************************/
        /// <summary>Returns the configured deterministic run identifier.</summary>
        /// <returns>The configured identifier.</returns>
        public Guid Create()
        {
            #region implementation

            return _runId;

            #endregion
        }

        #endregion
    }

    #endregion
}
