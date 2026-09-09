using System.Text.Json;
using Carrot.Cli.Common;
using Carrot.Cli.ISearch;
using Carrot.Cli.ISearch.Contracts;
using Carrot.Cli.Reporting;
using Xunit;

namespace Carrot.Cli.Tests.Reporting;

/**************************************************************/
/// <summary>Verifies ordered mapping of all walked generic iSearch records.</summary>
public sealed class SearchResultsReportMapperTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Ensures multiple service pages become one ordered dynamic worksheet table.</summary>
    [Fact]
    public async Task Create_CombinesWalkedPagesAndPreservesDynamicValues()
    {
        #region implementation

        var secondPage = createPage(
            2,
            null,
            new { title = "second", count = 2, extra = "appears later" });
        var session = new SearchResultPageSession(
            new FakeClient(secondPage),
            new SearchRequest
            {
                Dataset = "grants",
                Query = "example",
                Fields = ["title", "count"],
                Rows = 2
            },
            createPage(1, "next", new { title = "first", count = 1 }));

        await session.FetchNextAsync(CancellationToken.None);
        var request = new SearchResultsReportMapper().Create(session, "results.xlsx", overwrite: false);

        Assert.Equal(["ResultPage", "ResultOrdinal", "title", "count", "extra"], request.Columns.Select(column => column.Name));
        Assert.Equal(2, request.Rows.Count);
        Assert.Equal(1, request.Rows[0][0].Value);
        Assert.Equal(0, request.Rows[0][1].Value);
        Assert.Equal("first", request.Rows[0][2].Value);
        Assert.Equal(2, request.Rows[1][0].Value);
        Assert.Equal(1, request.Rows[1][1].Value);
        Assert.Equal("appears later", request.Rows[1][4].Value);
        Assert.Equal(2, session.WalkedResults.Count(result => result.ValueKind == JsonValueKind.Object));

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures scalar and nested records are retained through the Value column.</summary>
    [Fact]
    public void Create_NonObjectRecordUsesDeterministicValueColumn()
    {
        #region implementation

        var page = new SearchResponse
        {
            Cardinality = new SearchCardinality
            {
                TotalResults = 2,
                CurrentResults = 2,
                PageNumber = 1,
                TotalPages = 1
            },
            Results =
            [
                JsonSerializer.SerializeToElement("=unsafe"),
                JsonSerializer.SerializeToElement(new[] { 1, 2 })
            ]
        };
        var session = new SearchResultPageSession(new FakeClient(page), new SearchRequest
        {
            Dataset = "values",
            Query = "*:*",
            Fields = ["title"],
            Rows = 100
        }, page);

        var request = new SearchResultsReportMapper().Create(session, "results.xlsx", overwrite: false);

        Assert.Equal(["ResultPage", "ResultOrdinal", "title", "Value"], request.Columns.Select(column => column.Name));
        Assert.Equal(ExcelCellKind.Text, request.Rows[0][3].Kind);
        Assert.Equal("=unsafe", request.Rows[0][3].Value);
        Assert.Equal("[1,2]", request.Rows[1][3].Value);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one generic service page for mapper tests.</summary>
    /// <param name="pageNumber">The one-based service page number.</param>
    /// <param name="cursor">The optional continuation cursor.</param>
    /// <param name="record">The JSON record to include.</param>
    /// <returns>A valid synthetic response page.</returns>
    private static SearchResponse createPage(int pageNumber, string? cursor, object record) => new()
    {
        Cursor = cursor,
        Cardinality = new SearchCardinality
        {
            TotalResults = 2,
            CurrentResults = 1,
            PageNumber = pageNumber,
            TotalPages = 2
        },
        Results = [JsonSerializer.SerializeToElement(record)]
    };

    /**************************************************************/
    /// <summary>Provides the configured continuation page without making network calls.</summary>
    private sealed class FakeClient : IISearchApiClient
    {
        #region implementation

        private readonly SearchResponse _nextPage;

        /**************************************************************/
        /// <summary>Initializes the fake with its continuation response.</summary>
        /// <param name="nextPage">The response returned for continuation.</param>
        public FakeClient(SearchResponse nextPage) => _nextPage = nextPage;

        /**************************************************************/
        /// <summary>Returns an unused operation failure.</summary>
        /// <typeparam name="T">The unused operation value type.</typeparam>
        /// <returns>A deterministic failure.</returns>
        private static Task<OperationResult<T>> unused<T>() where T : class =>
            Task.FromResult(OperationResult<T>.Failure(
            [new OperationMessage { Code = "test.unused", Message = "unused", Severity = OperationMessageSeverity.Error }]));

        /**************************************************************/
        /// <summary>Returns an unused health failure.</summary>
        /// <param name="cancellationToken">The ignored cancellation token.</param>
        public Task<OperationResult<SearchHealthResponse>> GetHealthAsync(CancellationToken cancellationToken) => unused<SearchHealthResponse>();

        /**************************************************************/
        /// <summary>Returns an unused dataset failure.</summary>
        /// <param name="cancellationToken">The ignored cancellation token.</param>
        public Task<OperationResult<IReadOnlyList<string>>> GetDatasetsAsync(CancellationToken cancellationToken) => unused<IReadOnlyList<string>>();

        /**************************************************************/
        /// <summary>Returns an unused fields failure.</summary>
        /// <param name="dataset">The ignored dataset.</param>
        /// <param name="cancellationToken">The ignored cancellation token.</param>
        public Task<OperationResult<IReadOnlyList<SearchField>>> GetFieldsAsync(string dataset, CancellationToken cancellationToken) => unused<IReadOnlyList<SearchField>>();

        /**************************************************************/
        /// <summary>Returns an unused initial-search failure.</summary>
        /// <param name="request">The ignored request.</param>
        /// <param name="cancellationToken">The ignored cancellation token.</param>
        public Task<OperationResult<SearchResponse>> SearchAsync(SearchRequest request, CancellationToken cancellationToken) => unused<SearchResponse>();

        /**************************************************************/
        /// <summary>Returns the configured continuation page.</summary>
        /// <param name="request">The ignored request.</param>
        /// <param name="cursor">The ignored cursor.</param>
        /// <param name="nextPageNumber">The ignored page number.</param>
        /// <param name="cancellationToken">The ignored cancellation token.</param>
        public Task<OperationResult<SearchResponse>> SearchNextPageAsync(
            SearchRequest request,
            string cursor,
            int nextPageNumber,
            CancellationToken cancellationToken) =>
            Task.FromResult(OperationResult<SearchResponse>.Success(_nextPage));

        #endregion
    }

    #endregion
}
