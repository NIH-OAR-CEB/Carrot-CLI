using Carrot.Cli.Common;

namespace Carrot.Cli.Reporting;

/**************************************************************/
/// <summary>Coordinates iSearch aggregation mapping and shared workbook persistence.</summary>
/// <remarks>Only records already retained by the session are written; save never fetches another page.</remarks>
/// <seealso cref="ISearchResultsExporter"/>
/// <seealso cref="SearchResultsReportMapper"/>
internal sealed class SearchResultsExporter : ISearchResultsExporter
{
    #region implementation

    private readonly SearchResultsReportMapper _reportMapper;
    private readonly IExcelWorkbookWriter _workbookWriter;

    /**************************************************************/
    /// <summary>Initializes iSearch export orchestration with mapping and workbook boundaries.</summary>
    /// <param name="reportMapper">The walked-record mapper.</param>
    /// <param name="workbookWriter">The shared atomic workbook writer.</param>
    public SearchResultsExporter(
        SearchResultsReportMapper reportMapper,
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
    /// <summary>Saves all successfully walked iSearch records as one workbook.</summary>
    /// <param name="request">The retained session, normalized path, and overwrite policy.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>The absolute saved path or a structured expected write failure.</returns>
    public async Task<OperationResult<string>> SaveAsync(
        SaveISearchResultsRequest request,
        CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Session);

        try
        {
            var report = _reportMapper.Create(request.Session, request.OutputPath, request.Overwrite);
            await _workbookWriter.WriteAsync(report, cancellationToken).ConfigureAwait(false);
            return OperationResult<string>.Success(Path.GetFullPath(request.OutputPath));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cancellation remains exceptional so the owning interactive menu can return its
            // standard cancellation exit code rather than reporting a misleading write failure.
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
                Code = "isearch.report.write.failed",
                Message = $"The iSearch Excel report could not be saved: {exception.Message}",
                Severity = OperationMessageSeverity.Error
            }]);
        }

        #endregion
    }

    #endregion
}
