using Carrot.Cli.Common;
using Carrot.Cli.ISearch;
using Carrot.Cli.ISearch.Contracts;
using Xunit;

namespace Carrot.Cli.Tests.ISearch;

/**************************************************************/
/// <summary>Verifies one-step iSearch result-page state transitions.</summary>
public sealed class SearchResultPageSessionTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Ensures a successful fetch replaces the page and stops when the new cursor is absent.</summary>
    [Fact]
    public async Task FetchNextAsync_SuccessReplacesCurrentPage()
    {
        #region implementation

        var client = new FakeClient
        {
            NextPage = OperationResult<SearchResponse>.Success(createPage(2, null, "second"))
        };
        var firstPage = createPage(1, "next", "first");
        var session = new SearchResultPageSession(client, createRequest(), firstPage);

        var result = await session.FetchNextAsync(CancellationToken.None);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Same(result.Value, session.CurrentPage);
        Assert.Equal("second", session.CurrentPage.Results[0].GetProperty("title").GetString());
        Assert.False(session.CanFetchNextPage);
        Assert.Equal(1, client.NextPageCalls);
        Assert.Equal("next", client.LastCursor);
        Assert.Equal(2, client.LastPageNumber);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures a failed fetch preserves the last successful page for retry or exit.</summary>
    [Fact]
    public async Task FetchNextAsync_FailureRetainsCurrentPage()
    {
        #region implementation

        var firstPage = createPage(1, "next", "first");
        var client = new FakeClient
        {
            NextPage = OperationResult<SearchResponse>.Failure(
            [new OperationMessage
            {
                Code = "isearch.http",
                Message = "continuation failed",
                Severity = OperationMessageSeverity.Error
            }])
        };
        var session = new SearchResultPageSession(client, createRequest(), firstPage);

        var result = await session.FetchNextAsync(CancellationToken.None);

        Assert.Equal(OperationStatus.Failure, result.Status);
        Assert.Same(firstPage, session.CurrentPage);
        Assert.True(session.CanFetchNextPage);
        Assert.Equal(1, client.NextPageCalls);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures a response without a cursor cannot trigger a continuation request.</summary>
    [Fact]
    public async Task FetchNextAsync_WithoutCursorDoesNotCallClient()
    {
        #region implementation

        var client = new FakeClient();
        var session = new SearchResultPageSession(client, createRequest(), createPage(1, null, "only"));

        var result = await session.FetchNextAsync(CancellationToken.None);

        Assert.Equal(OperationStatus.Failure, result.Status);
        Assert.Equal("isearch.paging.unavailable", result.Messages[0].Code);
        Assert.Equal(0, client.NextPageCalls);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a stable synthetic query context for session tests.</summary>
    /// <returns>The bounded query request.</returns>
    private static SearchRequest createRequest() => new()
    {
        Dataset = "grants",
        Query = "vaccine",
        Fields = ["title"],
        DefaultOp = "AND",
        Rows = 10
    };

    /**************************************************************/
    /// <summary>Creates a generic response page with the requested service-page metadata.</summary>
    /// <param name="pageNumber">The service result-page number.</param>
    /// <param name="cursor">The optional next-page cursor.</param>
    /// <param name="title">The synthetic record title.</param>
    /// <returns>A valid one-record response page.</returns>
    private static SearchResponse createPage(int pageNumber, string? cursor, string title) => new()
    {
        Cursor = cursor,
        Cardinality = new SearchCardinality
        {
            TotalResults = 11,
            CurrentResults = 1,
            PageNumber = pageNumber,
            TotalPages = 2
        },
        Results = [System.Text.Json.JsonSerializer.SerializeToElement(new { title })]
    };

    /**************************************************************/
    /// <summary>Provides deterministic API results for page-session tests.</summary>
    private sealed class FakeClient : IISearchApiClient
    {
        #region implementation

        /**************************************************************/
        /// <summary>Gets or sets the continuation result returned by the fake.</summary>
        public OperationResult<SearchResponse> NextPage { get; init; } = OperationResult<SearchResponse>.Failure(
            [new OperationMessage { Code = "test.next-page", Message = "not configured", Severity = OperationMessageSeverity.Error }]);

        /**************************************************************/
        /// <summary>Gets the number of continuation calls.</summary>
        public int NextPageCalls { get; private set; }

        /**************************************************************/
        /// <summary>Gets the last cursor supplied to the fake.</summary>
        public string? LastCursor { get; private set; }

        /**************************************************************/
        /// <summary>Gets the last page number supplied to the fake.</summary>
        public int LastPageNumber { get; private set; }

        /**************************************************************/
        /// <summary>Returns an unused health failure.</summary>
        /// <param name="cancellationToken">The ignored cancellation token.</param>
        public Task<OperationResult<SearchHealthResponse>> GetHealthAsync(CancellationToken cancellationToken) => unused<SearchHealthResponse>();

        /**************************************************************/
        /// <summary>Returns an unused dataset failure.</summary>
        /// <param name="cancellationToken">The ignored cancellation token.</param>
        public Task<OperationResult<IReadOnlyList<string>>> GetDatasetsAsync(CancellationToken cancellationToken) => unused<IReadOnlyList<string>>();

        /**************************************************************/
        /// <summary>Returns an unused field failure.</summary>
        /// <param name="dataset">The unused dataset.</param>
        /// <param name="cancellationToken">The ignored cancellation token.</param>
        public Task<OperationResult<IReadOnlyList<SearchField>>> GetFieldsAsync(string dataset, CancellationToken cancellationToken) => unused<IReadOnlyList<SearchField>>();

        /**************************************************************/
        /// <summary>Returns an unused initial-search failure.</summary>
        /// <param name="request">The unused query request.</param>
        /// <param name="cancellationToken">The ignored cancellation token.</param>
        public Task<OperationResult<SearchResponse>> SearchAsync(SearchRequest request, CancellationToken cancellationToken) => unused<SearchResponse>();

        /**************************************************************/
        /// <summary>Records and returns the configured continuation response.</summary>
        /// <param name="request">The stable query context.</param>
        /// <param name="cursor">The current page cursor.</param>
        /// <param name="nextPageNumber">The expected next page number.</param>
        /// <param name="cancellationToken">The ignored cancellation token.</param>
        public Task<OperationResult<SearchResponse>> SearchNextPageAsync(
            SearchRequest request,
            string cursor,
            int nextPageNumber,
            CancellationToken cancellationToken)
        {
            NextPageCalls++;
            LastCursor = cursor;
            LastPageNumber = nextPageNumber;
            return Task.FromResult(NextPage);
        }

        /**************************************************************/
        /// <summary>Creates a deterministic unused-operation failure.</summary>
        /// <typeparam name="T">The unused operation value type.</typeparam>
        /// <returns>A failed operation result.</returns>
        private static Task<OperationResult<T>> unused<T>() where T : class =>
            Task.FromResult(OperationResult<T>.Failure(
                [new OperationMessage { Code = "test.unused", Message = "unused", Severity = OperationMessageSeverity.Error }]));

        #endregion
    }

    #endregion
}
