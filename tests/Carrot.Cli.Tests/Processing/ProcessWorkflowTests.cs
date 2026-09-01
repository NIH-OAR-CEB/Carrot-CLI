using Carrot.Cli.CarrotApi.Contracts;
using Carrot.Cli.CarrotApi;
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
/// <summary>Verifies named processing delegates one retained batch to reporting and artifact boundaries.</summary>
public sealed class ProcessWorkflowTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Verifies ready documents produce one workbook, two sidecars, and retain the processor run identifier.</summary>
    [Fact]
    public async Task ProcessAsync_ReadyDocuments_WritesCorrelatedArtifactsAndRetainsRunId()
    {
        #region implementation

        // Arrange
        var batch = createPreparedBatch();
        var runId = Guid.Parse("a1b2c3d4-e5f6-4a1b-8c9d-0e1f2a3b4c5d");
        var processedBatch = createProcessedBatch(batch, runId);
        var processor = new RecordingPreparedDocumentProcessor(OperationResult<ProcessedDocumentBatch>.Success(processedBatch));
        var jsonWriter = new RecordingJsonArtifactWriter();
        var excelWriter = new RecordingExcelReportWriter();
        var workflow = createWorkflow(new FakePreparationWorkflow(OperationResult<PreparedDocumentBatch>.Success(batch)), processor, jsonWriter, excelWriter);
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"carrot-process-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);
        try
        {
            // Act
            var result = await workflow.ProcessAsync(createRequest(outputDirectory), TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(OperationStatus.Success, result.Status);
            Assert.Equal(ExitCodes.Success, result.ExitCode);
            Assert.Equal(runId, result.RunId);
            Assert.Single(processor.Requests);
            Assert.Equal(3, result.ArtifactPaths.Count);
            Assert.Equal(2, result.Rows.Count);
            Assert.Equal(2, jsonWriter.Artifacts.Count);
            var report = Assert.Single(excelWriter.Requests);
            Assert.Equal(runId, report.Rows[0].RunId);
            Assert.Equal(runId, report.Rows[1].RunId);
            Assert.All(result.ArtifactPaths, path => Assert.Contains(runId.ToString("D"), path, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies partial preparation preserves useful output and the documented partial-success exit code.</summary>
    [Fact]
    public async Task ProcessAsync_PartialPreparation_ReturnsPartialSuccessAfterWritingArtifacts()
    {
        #region implementation

        // Arrange
        var batch = createPreparedBatch();
        var processor = new RecordingPreparedDocumentProcessor(OperationResult<ProcessedDocumentBatch>.Success(createProcessedBatch(batch, Guid.NewGuid())));
        var jsonWriter = new RecordingJsonArtifactWriter();
        var excelWriter = new RecordingExcelReportWriter();
        var workflow = createWorkflow(
            new FakePreparationWorkflow(OperationResult<PreparedDocumentBatch>.PartialSuccess(batch, [warning("preparation.extract", "One source failed.")])),
            processor,
            jsonWriter,
            excelWriter);

        // Act
        var result = await workflow.ProcessAsync(createRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OperationStatus.PartialSuccess, result.Status);
        Assert.Equal(ExitCodes.PartialSuccess, result.ExitCode);
        Assert.Single(processor.Requests);
        Assert.Single(excelWriter.Requests);
        Assert.Equal(2, jsonWriter.Artifacts.Count);

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies staged list diagnostics map to endpoint failure without writing artifacts.</summary>
    [Fact]
    public async Task ProcessAsync_ListFailure_ReturnsEndpointFailureWithoutArtifacts()
    {
        #region implementation

        // Arrange
        var processor = new RecordingPreparedDocumentProcessor(OperationResult<ProcessedDocumentBatch>.Failure(
            [error("processing.list.failure", "The Carrot service configuration could not be retrieved.")]));
        var jsonWriter = new RecordingJsonArtifactWriter();
        var excelWriter = new RecordingExcelReportWriter();
        var workflow = createWorkflow(new FakePreparationWorkflow(OperationResult<PreparedDocumentBatch>.Success(createPreparedBatch())), processor, jsonWriter, excelWriter);

        // Act
        var result = await workflow.ProcessAsync(createRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ExitCodes.EndpointFailure, result.ExitCode);
        Assert.Empty(result.ArtifactPaths);
        Assert.Empty(jsonWriter.Artifacts);
        Assert.Empty(excelWriter.Requests);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a workflow with fakes for preparation, cluster submission, and persistence boundaries.</summary>
    private static DocumentProcessingWorkflow createWorkflow(
        IDocumentPreparationWorkflow preparation,
        IPreparedDocumentProcessor processor,
        IJsonArtifactWriter jsonWriter,
        IExcelReportWriter excelWriter)
    {
        #region implementation

        var options = Options.Create(new CarrotCliOptions());
        return new DocumentProcessingWorkflow(
            preparation,
            new ClusteringConfigurationResolver(options),
            new ClusteringConfigurationValidator(),
            new UnusedApiClient(),
            new ClusterRequestFactory(options),
            jsonWriter,
            processor,
            new ProcessedDocumentReportMapper(options),
            excelWriter);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates the resolved request used by process workflow tests.</summary>
    private static ProcessRequest createRequest(string? outputPath = null) => new()
    {
        InputPath = Path.Combine(Path.GetTempPath(), "input.txt"),
        Endpoint = new Uri("https://carrot.example/service"),
        OutputPath = outputPath,
        Clustering = new ClusteringSelection { Algorithm = "Lingo", Language = "English" },
        Timeout = TimeSpan.FromSeconds(30)
    };

    /**************************************************************/
    /// <summary>Creates a small ordered prepared batch representative of shared document extraction output.</summary>
    private static PreparedDocumentBatch createPreparedBatch() => new()
    {
        Documents =
        [
            createDocument(0, "first.txt", "First title", "First content"),
            createDocument(1, "second.txt", "Second title", "Second content")
        ]
    };

    /**************************************************************/
    /// <summary>Creates one complete retained processor outcome with an explicit correlation identifier.</summary>
    private static ProcessedDocumentBatch createProcessedBatch(PreparedDocumentBatch prepared, Guid runId)
    {
        #region implementation

        var request = new ClusterRequestFactory(Options.Create(new CarrotCliOptions())).Create(
            prepared.Documents,
            new ClusteringConfiguration { Algorithm = "Lingo", Language = "English" });
        return new ProcessedDocumentBatch
        {
            RunId = runId,
            Endpoint = new Uri("https://carrot.example/service"),
            Request = request,
            Response = new ClusterResponse(),
            Rows = prepared.Documents.Select((document, index) => new ProcessedDocumentRow
            {
                CarrotDocumentIndex = index,
                PreparedDocument = document
            }).ToArray()
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one extracted source document for retained batch tests.</summary>
    private static ExtractedDocument createDocument(int index, string fileName, string title, string content) => new()
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

    /**************************************************************/
    /// <summary>Creates one safe error diagnostic for controlled dependency outcomes.</summary>
    private static OperationMessage error(string code, string message) => new()
    {
        Code = code,
        Message = message,
        Severity = OperationMessageSeverity.Error
    };

    /**************************************************************/
    /// <summary>Creates one safe warning diagnostic for partial preparation outcomes.</summary>
    private static OperationMessage warning(string code, string message) => new()
    {
        Code = code,
        Message = message,
        Severity = OperationMessageSeverity.Warning
    };

    /**************************************************************/
    /// <summary>Returns a configured preparation outcome without file-system discovery.</summary>
    private sealed class FakePreparationWorkflow(OperationResult<PreparedDocumentBatch> result) : IDocumentPreparationWorkflow
    {
        /**************************************************************/
        /// <summary>Returns the configured outcome after honoring cooperative cancellation.</summary>
        public Task<OperationResult<PreparedDocumentBatch>> PrepareAsync(PrepareDocumentsRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(result);
        }
    }

    /**************************************************************/
    /// <summary>Records exactly one prepared-item processing request and returns its configured result.</summary>
    private sealed class RecordingPreparedDocumentProcessor(OperationResult<ProcessedDocumentBatch> result) : IPreparedDocumentProcessor
    {
        /**************************************************************/
        /// <summary>Gets the submitted prepared-item requests.</summary>
        internal List<ProcessPreparedItemsRequest> Requests { get; } = [];

        /**************************************************************/
        /// <summary>Records and returns the configured processing outcome.</summary>
        public Task<OperationResult<ProcessedDocumentBatch>> ProcessAsync(ProcessPreparedItemsRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(result);
        }
    }

    /**************************************************************/
    /// <summary>Records all JSON artifacts without persisting them.</summary>
    private sealed class RecordingJsonArtifactWriter : IJsonArtifactWriter
    {
        /**************************************************************/
        /// <summary>Gets artifact paths and values supplied for JSON persistence.</summary>
        internal List<(string Path, object Artifact)> Artifacts { get; } = [];

        /**************************************************************/
        /// <summary>Records one JSON artifact write.</summary>
        public Task WriteAsync<TArtifact>(string path, TArtifact artifact, bool overwrite, CancellationToken cancellationToken)
        {
            Artifacts.Add((path, artifact!));
            return Task.CompletedTask;
        }
    }

    /**************************************************************/
    /// <summary>Records workbook requests without persisting them.</summary>
    private sealed class RecordingExcelReportWriter : IExcelReportWriter
    {
        /**************************************************************/
        /// <summary>Gets the report requests supplied for workbook persistence.</summary>
        internal List<ReportRequest> Requests { get; } = [];

        /**************************************************************/
        /// <summary>Records one workbook write.</summary>
        public Task WriteAsync(ReportRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.CompletedTask;
        }
    }

    /**************************************************************/
    /// <summary>Rejects API calls because process workflow tests isolate the prepared-processor seam.</summary>
    private sealed class UnusedApiClient : Carrot.Cli.CarrotApi.ICarrotApiClient
    {
        /**************************************************************/
        /// <summary>Rejects unexpected list calls.</summary>
        public Task<OperationResult<Carrot.Cli.CarrotApi.Contracts.ListResponse>> GetConfigurationAsync(Uri serviceEndpoint, TimeSpan timeout, bool? indent, CancellationToken cancellationToken) => throw new InvalidOperationException();

        /**************************************************************/
        /// <summary>Rejects unexpected cluster calls.</summary>
        public Task<OperationResult<ClusterResponse>> ClusterAsync(Uri serviceEndpoint, ClusterRequest request, string? template, TimeSpan timeout, bool? indent, CancellationToken cancellationToken) => throw new InvalidOperationException();
    }

    #endregion
}
