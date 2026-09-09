using System.Text.Json;
using Carrot.Cli.Common;
using Carrot.Cli.ISearch;
using Carrot.Cli.ISearch.Contracts;
using Carrot.Cli.Reporting;
using Xunit;

namespace Carrot.Cli.Tests.Reporting;

/**************************************************************/
/// <summary>Verifies iSearch export orchestration uses only retained pages and the shared writer.</summary>
public sealed class SearchResultsExporterTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Ensures saving maps the retained session and forwards one generic workbook request.</summary>
    [Fact]
    public async Task SaveAsync_UsesRetainedSessionWithoutCallingSearch()
    {
        #region implementation

        var page = new SearchResponse
        {
            Cardinality = new SearchCardinality
            {
                TotalResults = 1,
                CurrentResults = 1,
                PageNumber = 1,
                TotalPages = 1
            },
            Results = [JsonSerializer.SerializeToElement(new { id = 7 })]
        };
        var session = new SearchResultPageSession(
            new UnusedSearchClient(),
            new SearchRequest
            {
                Dataset = "grants",
                Query = "example",
                Fields = ["id"],
                Rows = 100
            },
            page);
        var writer = new CapturingWorkbookWriter();
        var exporter = new SearchResultsExporter(new SearchResultsReportMapper(), writer);
        var outputPath = Path.Combine(Path.GetTempPath(), "isearch-results.xlsx");

        var result = await exporter.SaveAsync(
            new SaveISearchResultsRequest
            {
                Session = session,
                OutputPath = outputPath,
                Overwrite = true
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(Path.GetFullPath(outputPath), result.Value);
        Assert.NotNull(writer.Request);
        Assert.Single(writer.Request.Rows);
        Assert.Equal("7", writer.Request.Rows[0][2].Value?.ToString());

        #endregion
    }

    /**************************************************************/
    /// <summary>Captures the shared workbook request without touching the filesystem.</summary>
    private sealed class CapturingWorkbookWriter : IExcelWorkbookWriter
    {
        #region implementation

        /**************************************************************/
        /// <summary>Gets the request received by the shared writer.</summary>
        public ExcelWorkbookRequest? Request { get; private set; }

        /**************************************************************/
        /// <summary>Captures one workbook request.</summary>
        /// <param name="request">The generic workbook request.</param>
        /// <param name="cancellationToken">The ignored cancellation token.</param>
        /// <returns>A completed write task.</returns>
        public Task WriteAsync(ExcelWorkbookRequest request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.CompletedTask;
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Provides failures for API calls that must not occur during export.</summary>
    private sealed class UnusedSearchClient : IISearchApiClient
    {
        #region implementation

        /**************************************************************/
        /// <summary>Throws if export unexpectedly requests health.</summary>
        /// <param name="cancellationToken">The ignored cancellation token.</param>
        public Task<OperationResult<SearchHealthResponse>> GetHealthAsync(CancellationToken cancellationToken) => unexpected<SearchHealthResponse>();

        /**************************************************************/
        /// <summary>Throws if export unexpectedly requests datasets.</summary>
        /// <param name="cancellationToken">The ignored cancellation token.</param>
        public Task<OperationResult<IReadOnlyList<string>>> GetDatasetsAsync(CancellationToken cancellationToken) => unexpected<IReadOnlyList<string>>();

        /**************************************************************/
        /// <summary>Throws if export unexpectedly requests fields.</summary>
        /// <param name="dataset">The ignored dataset.</param>
        /// <param name="cancellationToken">The ignored cancellation token.</param>
        public Task<OperationResult<IReadOnlyList<SearchField>>> GetFieldsAsync(string dataset, CancellationToken cancellationToken) => unexpected<IReadOnlyList<SearchField>>();

        /**************************************************************/
        /// <summary>Throws if export unexpectedly submits a new search.</summary>
        /// <param name="request">The ignored request.</param>
        /// <param name="cancellationToken">The ignored cancellation token.</param>
        public Task<OperationResult<SearchResponse>> SearchAsync(SearchRequest request, CancellationToken cancellationToken) => unexpected<SearchResponse>();

        /**************************************************************/
        /// <summary>Throws if export unexpectedly fetches another page.</summary>
        /// <param name="request">The ignored request.</param>
        /// <param name="cursor">The ignored cursor.</param>
        /// <param name="nextPageNumber">The ignored page number.</param>
        /// <param name="cancellationToken">The ignored cancellation token.</param>
        public Task<OperationResult<SearchResponse>> SearchNextPageAsync(
            SearchRequest request,
            string cursor,
            int nextPageNumber,
            CancellationToken cancellationToken) => unexpected<SearchResponse>();

        /**************************************************************/
        /// <summary>Creates a test failure for an operation that should remain unused.</summary>
        /// <typeparam name="T">The unused operation value type.</typeparam>
        /// <returns>A task containing an unexpected-call failure.</returns>
        private static Task<OperationResult<T>> unexpected<T>() where T : class =>
            Task.FromException<OperationResult<T>>(new InvalidOperationException("The API must not be called during export."));

        #endregion
    }

    #endregion
}
