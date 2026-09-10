using Carrot.Cli.Common;

namespace Carrot.Cli.Reporting;

/**************************************************************/
/// <summary>Coordinates categorized iSearch mapping and shared workbook persistence.</summary>
/// <remarks>Saving consumes only the completed categorized batch and never calls iSearch or Carrot.</remarks>
/// <seealso cref="ICategorizedISearchResultsExporter"/>
/// <seealso cref="CategorizedISearchResultsReportMapper"/>
internal sealed class CategorizedISearchResultsExporter : ICategorizedISearchResultsExporter
{
    #region implementation

    private readonly CategorizedISearchResultsReportMapper _reportMapper;
    private readonly IExcelWorkbookWriter _workbookWriter;

    /**************************************************************/
    /// <summary>Initializes categorized iSearch export orchestration.</summary>
    /// <param name="reportMapper">The categorized-row mapper.</param>
    /// <param name="workbookWriter">The shared atomic workbook writer.</param>
    public CategorizedISearchResultsExporter(
        CategorizedISearchResultsReportMapper reportMapper,
        IExcelWorkbookWriter workbookWriter)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(reportMapper);
        ArgumentNullException.ThrowIfNull(workbookWriter);
        _reportMapper = reportMapper;
        _workbookWriter = workbookWriter;

        #endregion
    }

    /**************************************************************/
    /// <summary>Maps and saves one categorized iSearch batch.</summary>
    /// <param name="request">The categorized batch, destination, and overwrite policy.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>The absolute saved path or a structured expected failure.</returns>
    public async Task<OperationResult<string>> SaveAsync(
        SaveCategorizedISearchResultsRequest request,
        CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var workbookRequest = _reportMapper.Create(request.Batch, request.OutputPath, request.Overwrite);
            await _workbookWriter.WriteAsync(workbookRequest, cancellationToken).ConfigureAwait(false);
            return OperationResult<string>.Success(Path.GetFullPath(request.OutputPath));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or ArgumentException
            or NotSupportedException
            or System.Text.Json.JsonException)
        {
            return OperationResult<string>.Failure(
            [new OperationMessage
            {
                Code = "isearch.categorized-report.write.failed",
                Message = $"The categorized iSearch Excel report could not be saved: {exception.Message}",
                Severity = OperationMessageSeverity.Error
            }]);
        }

        #endregion
    }

    #endregion
}
