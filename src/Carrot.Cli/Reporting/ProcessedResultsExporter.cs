using Carrot.Cli.Common;

namespace Carrot.Cli.Reporting;

/**************************************************************/
/// <summary>
/// Coordinates processed-result mapping and workbook persistence behind one application boundary.
/// </summary>
/// <remarks>
/// Expected filesystem and workbook failures become structured results so the interactive menu
/// can retain the in-memory batch. Caller cancellation remains exceptional and propagates.
/// </remarks>
/// <seealso cref="IProcessedResultsExporter"/>
/// <seealso cref="ProcessedDocumentReportMapper"/>
/// <seealso cref="IExcelReportWriter"/>
internal sealed class ProcessedResultsExporter : IProcessedResultsExporter
{
    #region implementation

    private readonly ProcessedDocumentReportMapper _reportMapper;
    private readonly IExcelReportWriter _excelWriter;

    /**************************************************************/
    /// <summary>Initializes export orchestration with its deterministic mapper and workbook writer.</summary>
    /// <param name="reportMapper">The retained-batch-to-report mapper.</param>
    /// <param name="excelWriter">The atomic Excel persistence boundary.</param>
    public ProcessedResultsExporter(
        ProcessedDocumentReportMapper reportMapper,
        IExcelReportWriter excelWriter)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(reportMapper);
        ArgumentNullException.ThrowIfNull(excelWriter);
        _reportMapper = reportMapper;
        _excelWriter = excelWriter;

        #endregion
    }

    /**************************************************************/
    /// <summary>Maps and saves one processed result without contacting Carrot or rereading source files.</summary>
    /// <param name="request">The retained batch, normalized output path, and overwrite policy.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>A successful absolute path or a structured expected write failure.</returns>
    public async Task<OperationResult<string>> SaveAsync(
        SaveProcessedResultsRequest request,
        CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var reportRequest = _reportMapper.Create(request.Batch, request.OutputPath, request.Overwrite);
            await _excelWriter.WriteAsync(reportRequest, cancellationToken).ConfigureAwait(false);
            return OperationResult<string>.Success(Path.GetFullPath(request.OutputPath));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or ArgumentException)
        {
            return OperationResult<string>.Failure(
            [
                new OperationMessage
                {
                    Code = "report.write.failed",
                    Message = $"The Excel report could not be saved: {exception.Message}",
                    Severity = OperationMessageSeverity.Error
                }
            ]);
        }

        #endregion
    }

    #endregion
}
