using Carrot.Cli.Common;
using Carrot.Cli.ISearch;
using Carrot.Cli.ISearch.Contracts;
using Xunit;

namespace Carrot.Cli.Tests.ISearch;

/**************************************************************/
/// <summary>Verifies one-step and all-pages iSearch result-page state transitions.</summary>
/// <remarks>
/// These tests use a deterministic API fake so session invariants can be checked without a live
/// credentialed request. They distinguish committed aggregate state from a response that failed before
/// adoption and verify that progress is published only for accepted pages.
/// </remarks>
/// <seealso cref="SearchResultPageSession"/>
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
        Assert.Equal(2, session.WalkedPages.Count);
        Assert.Equal(["first", "second"], session.WalkedResults.Select(item => item.GetProperty("title").GetString()));

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
        Assert.Single(session.WalkedPages);
        Assert.Equal("first", session.WalkedResults[0].GetProperty("title").GetString());

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
    /// <summary>Ensures the all-pages walk fetches every remaining page in cursor order.</summary>
    /// <remarks>
    /// Three one-record pages make the expected snapshots observable: the initial callback reports one
    /// loaded record, then each accepted continuation advances the page and count exactly once.
    /// </remarks>
    [Fact]
    public async Task FetchAllAsync_FetchesRemainingPagesAndReportsCommittedProgress()
    {
        #region implementation

        var client = new FakeClient
        {
            NextPages = new Queue<OperationResult<SearchResponse>>(
            [
                OperationResult<SearchResponse>.Success(createPage(2, "page-three", "second", 3, 3)),
                OperationResult<SearchResponse>.Success(createPage(3, null, "third", 3, 3))
            ])
        };
        var session = new SearchResultPageSession(
            client,
            createRequest(),
            createPage(1, "page-two", "first", 3, 3));
        var progress = new List<SearchPageWalkProgress>();

        var result = await session.FetchAllAsync(progress.Add, CancellationToken.None);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(2, client.NextPageCalls);
        Assert.Equal([1, 2, 3], progress.Select(item => item.DataPage));
        Assert.Equal([1, 2, 3], progress.Select(item => item.LoadedRecords));
        Assert.Equal(100D, progress[^1].Percentage);
        Assert.True(progress[^1].IsComplete);
        Assert.Equal(["first", "second", "third"], session.WalkedResults
            .Select(item => item.GetProperty("title").GetString()));
        Assert.Equal(3, session.CurrentPage.Cardinality.PageNumber);
        Assert.False(session.CanFetchNextPage);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures an expected failure retains pages accepted before the failed request.</summary>
    /// <remarks>
    /// The queued success followed by failure protects the retry boundary: a failed later request must
    /// not remove the successfully committed page or report progress for data that was not accepted.
    /// </remarks>
    [Fact]
    public async Task FetchAllAsync_FailureRetainsCommittedPagesAndPartialProgress()
    {
        #region implementation

        var client = new FakeClient
        {
            NextPages = new Queue<OperationResult<SearchResponse>>(
            [
                OperationResult<SearchResponse>.Success(createPage(2, "page-three", "second", 3, 3)),
                OperationResult<SearchResponse>.Failure(
                [new OperationMessage
                {
                    Code = "isearch.http",
                    Message = "continuation failed",
                    Severity = OperationMessageSeverity.Error
                }])
            ])
        };
        var session = new SearchResultPageSession(
            client,
            createRequest(),
            createPage(1, "page-two", "first", 3, 3));
        var progress = new List<SearchPageWalkProgress>();

        var result = await session.FetchAllAsync(progress.Add, CancellationToken.None);

        Assert.Equal(OperationStatus.Failure, result.Status);
        Assert.Equal("isearch.http", result.Messages[0].Code);
        Assert.Equal([1, 2], progress.Select(item => item.DataPage));
        Assert.Equal(2, session.WalkedResults.Count);
        Assert.Equal(2, session.CurrentPage.Cardinality.PageNumber);
        Assert.True(session.CanFetchNextPage);
        Assert.Equal(2, client.NextPageCalls);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures an unchanged cursor cannot make a walk loop or mutate retained state.</summary>
    /// <remarks>
    /// The fake returns the same cursor as the current page. The session must fail before appending the
    /// duplicate response, leaving exactly one retained page and one continuation call.
    /// </remarks>
    [Fact]
    public async Task FetchAllAsync_UnchangedCursorReturnsFailureWithoutAppendingPage()
    {
        #region implementation

        var client = new FakeClient
        {
            NextPage = OperationResult<SearchResponse>.Success(createPage(2, "page-two", "duplicate", 3, 3))
        };
        var session = new SearchResultPageSession(
            client,
            createRequest(),
            createPage(1, "page-two", "first", 3, 3));

        var result = await session.FetchAllAsync(progress: null, CancellationToken.None);

        Assert.Equal(OperationStatus.Failure, result.Status);
        Assert.Equal("isearch.paging.cursor-unchanged", result.Messages[0].Code);
        Assert.Single(session.WalkedPages);
        Assert.Equal("first", session.WalkedResults[0].GetProperty("title").GetString());
        Assert.Equal(1, client.NextPageCalls);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures an empty result set is a complete no-op with no continuation request.</summary>
    /// <remarks>An empty successful query has no records remaining to fetch and therefore reports 100 percent.</remarks>
    [Fact]
    public async Task FetchAllAsync_EmptyResultIsCompleteWithoutCallingClient()
    {
        #region implementation

        var client = new FakeClient();
        var session = new SearchResultPageSession(client, createRequest(), new SearchResponse
        {
            Cardinality = new SearchCardinality
            {
                TotalResults = 0,
                CurrentResults = 0,
                PageNumber = 0,
                TotalPages = 0
            }
        });
        var progress = new List<SearchPageWalkProgress>();

        var result = await session.FetchAllAsync(progress.Add, CancellationToken.None);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(100D, Assert.Single(progress).Percentage);
        Assert.True(progress[0].IsComplete);
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
    /// <param name="totalResults">The stable query total used by progress assertions.</param>
    /// <param name="totalPages">The total service-page count used by continuation guards.</param>
    /// <returns>A valid one-record response page with the requested metadata.</returns>
    /// <remarks>Optional totals preserve the compact setup used by the original one-step tests.</remarks>
    private static SearchResponse createPage(
        int pageNumber,
        string? cursor,
        string title,
        int totalResults = 11,
        int totalPages = 2) => new()
        {
            Cursor = cursor,
            Cardinality = new SearchCardinality
            {
                TotalResults = totalResults,
                CurrentResults = 1,
                PageNumber = pageNumber,
                TotalPages = totalPages
            },
            Results = [System.Text.Json.JsonSerializer.SerializeToElement(new { title })]
        };

    /**************************************************************/
    /// <summary>Provides deterministic API results for page-session tests.</summary>
    private sealed class FakeClient : IISearchApiClient
    {
        #region implementation

        /**************************************************************/
        /// <summary>Gets or sets the fallback continuation result returned by the fake.</summary>
        /// <remarks>This result is used when a test has not supplied a queued response.</remarks>
        public OperationResult<SearchResponse> NextPage { get; init; } = OperationResult<SearchResponse>.Failure(
            [new OperationMessage { Code = "test.next-page", Message = "not configured", Severity = OperationMessageSeverity.Error }]);

        /**************************************************************/
        /// <summary>Gets or sets queued continuation results for multi-page walk tests.</summary>
        /// <remarks>Responses are dequeued in request order, allowing cursor sequence and partial failure assertions.</remarks>
        public Queue<OperationResult<SearchResponse>> NextPages { get; init; } = [];

        /**************************************************************/
        /// <summary>Gets the number of continuation calls.</summary>
        /// <remarks>The count proves display-only operations do not accidentally issue network requests in session tests.</remarks>
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
        /// <summary>Records and returns the next deterministic continuation response.</summary>
        /// <param name="request">The stable query context.</param>
        /// <param name="cursor">The current page cursor.</param>
        /// <param name="nextPageNumber">The expected next page number.</param>
        /// <param name="cancellationToken">The ignored cancellation token.</param>
        /// <returns>The next queued response, or the configured fallback when the queue is empty.</returns>
        /// <remarks>The fake records the cursor and expected page number so request construction can be asserted.</remarks>
        public Task<OperationResult<SearchResponse>> SearchNextPageAsync(
            SearchRequest request,
            string cursor,
            int nextPageNumber,
            CancellationToken cancellationToken)
        {
            NextPageCalls++;
            LastCursor = cursor;
            LastPageNumber = nextPageNumber;
            return Task.FromResult(NextPages.Count > 0 ? NextPages.Dequeue() : NextPage);
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
