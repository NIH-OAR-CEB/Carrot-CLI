using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Carrot.Cli.Reporting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Carrot.Cli.Tests.Reporting;

/**************************************************************/
/// <summary>Verifies processed-result export orchestration, error translation, and cancellation.</summary>
public sealed class ProcessedResultsExporterTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Verifies the public save method maps and forwards the exact destination and overwrite policy.</summary>
    [Fact]
    public async Task SaveAsync_ValidRequest_ForwardsMappedWorkbookAndReturnsAbsolutePath()
    {
        #region implementation

        // Arrange
        var outputPath = Path.GetFullPath("processed-export.xlsx");
        var writer = new CapturingExcelReportWriter();
        var exporter = createExporter(writer);
        var batch = ReportingTestData.CreateProcessedBatch();

        // Act
        var result = await exporter.SaveAsync(
            new SaveProcessedResultsRequest
            {
                Batch = batch,
                OutputPath = outputPath,
                Overwrite = true
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(outputPath, result.Value);
        var report = Assert.Single(writer.Requests);
        Assert.Equal(outputPath, report.OutputPath);
        Assert.True(report.Overwrite);
        Assert.Equal(ReportingTestData.RunId, Assert.Single(report.Rows).RunId);

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies expected infrastructure failures become safe structured export failures.</summary>
    [Fact]
    public async Task SaveAsync_WriterIoFailure_ReturnsStructuredFailure()
    {
        #region implementation

        // Arrange
        var exporter = createExporter(new ThrowingExcelReportWriter(new IOException("Workbook is locked.")));

        // Act
        var result = await exporter.SaveAsync(
            new SaveProcessedResultsRequest
            {
                Batch = ReportingTestData.CreateProcessedBatch(),
                OutputPath = Path.GetFullPath("locked.xlsx")
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OperationStatus.Failure, result.Status);
        Assert.Null(result.Value);
        var message = Assert.Single(result.Messages);
        Assert.Equal("report.write.failed", message.Code);
        Assert.Contains("Workbook is locked", message.Message, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies caller cancellation from the writer propagates instead of becoming a failure result.</summary>
    [Fact]
    public async Task SaveAsync_WriterCancellation_PropagatesCancellation()
    {
        #region implementation

        // Arrange
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var exporter = createExporter(new ThrowingExcelReportWriter(new OperationCanceledException(cancellationSource.Token)));

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => exporter.SaveAsync(
            new SaveProcessedResultsRequest
            {
                Batch = ReportingTestData.CreateProcessedBatch(),
                OutputPath = Path.GetFullPath("cancelled.xlsx")
            },
            cancellationSource.Token));

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates the exporter with a real deterministic mapper and controlled writer.</summary>
    /// <param name="writer">The controlled workbook boundary.</param>
    /// <returns>The configured exporter.</returns>
    private static ProcessedResultsExporter createExporter(IExcelReportWriter writer)
    {
        #region implementation

        return new ProcessedResultsExporter(
            new ProcessedDocumentReportMapper(Options.Create(new CarrotCliOptions())),
            writer);

        #endregion
    }

    /**************************************************************/
    /// <summary>Captures every report supplied through the public writer contract.</summary>
    private sealed class CapturingExcelReportWriter : IExcelReportWriter
    {
        #region implementation

        /**************************************************************/
        /// <summary>Gets captured report requests in invocation order.</summary>
        internal List<ReportRequest> Requests { get; } = [];

        /**************************************************************/
        /// <summary>Captures one report and completes without filesystem work.</summary>
        public Task WriteAsync(ReportRequest request, CancellationToken cancellationToken)
        {
            #region implementation

            Requests.Add(request);
            return Task.CompletedTask;

            #endregion
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Throws one configured exception through the public writer contract.</summary>
    private sealed class ThrowingExcelReportWriter : IExcelReportWriter
    {
        #region implementation

        private readonly Exception _exception;

        /**************************************************************/
        /// <summary>Initializes the writer with the exception thrown by each call.</summary>
        /// <param name="exception">The controlled exception.</param>
        internal ThrowingExcelReportWriter(Exception exception)
        {
            #region implementation

            _exception = exception;

            #endregion
        }

        /**************************************************************/
        /// <summary>Throws the configured exception.</summary>
        public Task WriteAsync(ReportRequest request, CancellationToken cancellationToken)
        {
            #region implementation

            return Task.FromException(_exception);

            #endregion
        }

        #endregion
    }

    #endregion
}
