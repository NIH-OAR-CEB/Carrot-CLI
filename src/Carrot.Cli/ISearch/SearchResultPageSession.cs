using System.Text.Json;
using Carrot.Cli.Common;
using Carrot.Cli.ISearch.Contracts;

namespace Carrot.Cli.ISearch;

/**************************************************************/
/// <summary>Maintains a bounded iSearch result-page transition and sequential walk.</summary>
/// <remarks>
/// The session keeps the original query context and latest validated response together so an
/// interactive pager can advance or exhaust the same cursor and page invariants. It retains accepted
/// pages for the current visit, but does not persist records or issue requests outside the API client.
/// The initial page contributes to the retained aggregate immediately. Continuation pages are adopted
/// atomically from the caller's perspective: a failed or contradictory response leaves the current page,
/// walked pages, walked records, and retry cursor at their last committed values. Retention is bounded
/// by the existing Excel-safe maximum so an explicit export cannot silently truncate the walk.
/// </remarks>
/// <seealso cref="IISearchApiClient"/>
/// <seealso cref="SearchRequest"/>
/// <seealso cref="SearchResponse"/>
internal sealed class SearchResultPageSession
{
    #region implementation

    private const int MaximumWalkedResults = 1_048_575;
    private readonly IISearchApiClient _client;
    private readonly SearchRequest _request;
    private readonly List<SearchResponse> _walkedPages;
    private readonly List<JsonElement> _walkedResults;
    private readonly int _totalResults;
    private bool _reachedTerminalResponse;

    /**************************************************************/
    /// <summary>Initializes a page session from a stable query and its first successful response.</summary>
    /// <param name="client">The authenticated iSearch API boundary used for continuation requests.</param>
    /// <param name="request">The unchanged database, query, fields, operator, and row-limit context.</param>
    /// <param name="initialPage">The validated first response to expose as the current page.</param>
    /// <param name="returnDataset">The configured return-dataset label, when the interactive flow supplies one.</param>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
    /// <remarks>
    /// The constructor treats <paramref name="initialPage"/> as already accepted and uses its total
    /// result count as the denominator for all later progress snapshots. No network request is made.
    /// </remarks>
    /// <seealso cref="FetchNextAsync"/>
    /// <seealso cref="FetchAllAsync"/>
    public SearchResultPageSession(
        IISearchApiClient client,
        SearchRequest request,
        SearchResponse initialPage,
        string? returnDataset = null)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(initialPage);

        _client = client;
        _request = request;
        ReturnDataset = returnDataset;
        CurrentPage = initialPage;
        _walkedPages = [initialPage];
        _walkedResults = initialPage.Results.ToList();
        _totalResults = initialPage.Cardinality.TotalResults;
        _reachedTerminalResponse = initialPage.Results.Count == 0 && _totalResults > 0;

        #endregion
    }

    /**************************************************************/
    /// <summary>Gets the latest successfully validated service response.</summary>
    /// <remarks>
    /// The value starts with the initial response and changes only after a continuation response passes
    /// total-count, cursor, empty-page, capacity, and aggregate-count checks.
    /// </remarks>
    public SearchResponse CurrentPage { get; private set; }

    /**************************************************************/
    /// <summary>Gets the unchanged request context used for every walked result page.</summary>
    /// <remarks>
    /// The pager passes this same context to the API client for each continuation, preserving the selected
    /// dataset, query, fields, operator, row limit, and other request values across the entire walk.
    /// </remarks>
    /// <seealso cref="IISearchApiClient.SearchNextPageAsync"/>
    public SearchRequest Request => _request;

    /**************************************************************/
    /// <summary>Gets every successfully walked service page in result-page order.</summary>
    /// <remarks>
    /// The collection includes the initial response and only pages that have been fully accepted. It is
    /// retained for explicit export and remains unchanged when a later request fails.
    /// </remarks>
    /// <seealso cref="WalkedResults"/>
    public IReadOnlyList<SearchResponse> WalkedPages => _walkedPages.AsReadOnly();

    /**************************************************************/
    /// <summary>Gets every record from every successfully walked page in service order.</summary>
    /// <remarks>
    /// Records are preserved exactly as returned, including duplicate records across pages. The list is
    /// bounded by <c>1,048,575</c> records so an Excel export can represent every retained record.
    /// </remarks>
    /// <seealso cref="WalkedPages"/>
    public IReadOnlyList<JsonElement> WalkedResults => _walkedResults.AsReadOnly();

    /**************************************************************/
    /// <summary>Gets the exact live dataset associated with the stable query context.</summary>
    /// <remarks>This value is copied from <see cref="Request"/> and is never changed during paging.</remarks>
    public string Dataset => _request.Dataset;

    /**************************************************************/
    /// <summary>Gets the configured return-dataset label associated with this interactive result view.</summary>
    /// <remarks>The label is presentation and export metadata; it does not replace the live API dataset.</remarks>
    public string? ReturnDataset { get; }

    /**************************************************************/
    /// <summary>Gets whether the current service response can advance to another result page.</summary>
    /// <remarks>
    /// Both a service cursor and remaining cardinality pages are required. Terminal display-page
    /// counts are intentionally excluded because they describe only how records fit on a screen.
    /// The property also becomes false when the stable record total has been accepted or a terminal
    /// response has been observed, preventing redundant requests after successful completion.
    /// </remarks>
    public bool CanFetchNextPage => !_reachedTerminalResponse
        && _walkedResults.Count < _totalResults
        && !string.IsNullOrWhiteSpace(CurrentPage.Cursor)
        && CurrentPage.Cardinality.PageNumber > 0
        && CurrentPage.Cardinality.PageNumber < CurrentPage.Cardinality.TotalPages;

    /**************************************************************/
    /// <summary>Gets the current record-based progress snapshot for this query visit.</summary>
    /// <remarks>
    /// The returned value is a new snapshot over the current committed state. An empty result set is
    /// complete at 100 percent because there are no records to fetch.
    /// </remarks>
    /// <seealso cref="SearchPageWalkProgress"/>
    public SearchPageWalkProgress WalkProgress => createProgress();

    /**************************************************************/
    /// <summary>Fetches and adopts exactly one subsequent service result page.</summary>
    /// <param name="cancellationToken">The token that cancels the continuation request.</param>
    /// <returns>The new current page on success or a safe operation failure.</returns>
    /// <remarks>
    /// A failed request leaves <see cref="CurrentPage"/> unchanged, allowing the interactive pager to
    /// retry without losing the last validated record chunk. Expected service-contract failures are
    /// returned as <see cref="OperationResult{T}"/> messages; cancellation is allowed to propagate from
    /// the API client rather than being converted into an ordinary paging failure.
    /// </remarks>
    /// <seealso cref="IISearchApiClient.SearchNextPageAsync"/>
    public async Task<OperationResult<SearchResponse>> FetchNextAsync(CancellationToken cancellationToken)
    {
        #region implementation

        if (!CanFetchNextPage)
        {
            // Refuse a missing cursor or exhausted cardinality locally so a terminal page cannot
            // accidentally repeat the initial query or create an invalid next-page number.
            return OperationResult<SearchResponse>.Failure(
            [new OperationMessage
            {
                Code = "isearch.paging.unavailable",
                Message = "iSearch has no next result page available.",
                Severity = OperationMessageSeverity.Error
            }]);
        }

        var nextPageNumber = CurrentPage.Cardinality.PageNumber + 1;
        var result = await _client.SearchNextPageAsync(
            _request,
            CurrentPage.Cursor!,
            nextPageNumber,
            cancellationToken).ConfigureAwait(false);

        // Keep the previous page until the complete continuation response has passed API-boundary
        // validation; callers can safely retry or leave the pager after any expected failure.
        if (result.Status == OperationStatus.Success)
        {
            var nextPage = result.Value!;

            // Every continuation belongs to the original query snapshot. Rejecting a changed total
            // before mutation keeps the progress denominator stable and prevents a false completion.
            if (nextPage.Cardinality.TotalResults != _totalResults)
            {
                return failure(
                    "isearch.paging.inconsistent-total",
                    "iSearch changed the total result count during the page walk.");
            }

            // A zero-record continuation before the stable total is reached is an incomplete walk,
            // not a successful terminal page that can be presented as fully loaded.
            if (nextPage.Results.Count == 0)
            {
                return failure(
                    "isearch.paging.incomplete",
                    "iSearch returned an empty page before all result records were loaded.");
            }

            // An unchanged cursor would cause repeated calls to retrieve the same page indefinitely.
            // Leave the last accepted page available so the operator can retry or leave safely.
            if (string.Equals(nextPage.Cursor, CurrentPage.Cursor, StringComparison.Ordinal))
            {
                return failure(
                    "isearch.paging.cursor-unchanged",
                    "iSearch returned an unchanged continuation cursor before the result set was complete.");
            }

            if (_walkedResults.Count > MaximumWalkedResults - nextPage.Results.Count)
            {
                // Excel cannot represent more than 1,048,575 data rows beneath a header, so reject
                // the next page before mutating state rather than saving an incomplete walk later.
                return OperationResult<SearchResponse>.Failure(
                [new OperationMessage
                {
                    Code = "isearch.paging.limit",
                    Message = "The walked iSearch results exceed Excel's maximum worksheet row capacity.",
                    Severity = OperationMessageSeverity.Error
                }]);
            }

            if (_walkedResults.Count > _totalResults - nextPage.Results.Count)
            {
                // Do not accept more rows than the stable service total; preserving this invariant is
                // required for both accurate progress and a trustworthy all-pages completion result.
                return failure(
                    "isearch.paging.inconsistent-count",
                    "iSearch returned more records than its total result count.");
            }

            // Append before exposing the new current page so the session's page and aggregate state
            // advance as one transition; a failed operation never changes either collection.
            _walkedPages.Add(nextPage);
            _walkedResults.AddRange(nextPage.Results);
            CurrentPage = nextPage;

            // A blank cursor or a complete record aggregate makes another request unnecessary even
            // when cardinality metadata still advertises a nominal later page.
            _reachedTerminalResponse = string.IsNullOrWhiteSpace(nextPage.Cursor)
                || _walkedResults.Count >= _totalResults;
        }

        return result;

        #endregion
    }

    /**************************************************************/
    /// <summary>Fetches every remaining service page in sequence and reports committed progress.</summary>
    /// <param name="progress">The optional callback invoked before the walk and after each accepted page.</param>
    /// <param name="cancellationToken">The token that cancels pacing or continuation requests.</param>
    /// <returns>The final current page on complete success or a safe failure retaining accepted pages.</returns>
    /// <remarks>
    /// Each iteration delegates to <see cref="FetchNextAsync"/> so authentication, pacing, retries,
    /// response validation, cursor handling, and cancellation remain owned by the existing boundaries.
    /// The callback is invoked synchronously with the initial state and after each committed page, so a
    /// consumer never receives a progress page number that is ahead of the retained records. If a later
    /// request fails, the method returns that failure after preserving all prior successful pages.
    /// </remarks>
    /// <seealso cref="SearchPageWalkProgress"/>
    /// <seealso cref="IISearchApiClient.SearchNextPageAsync"/>
    public async Task<OperationResult<SearchResponse>> FetchAllAsync(
        Action<SearchPageWalkProgress>? progress,
        CancellationToken cancellationToken)
    {
        #region implementation

        progress?.Invoke(WalkProgress);

        // A zero-result response or an already-complete aggregate is a successful no-op; there is no
        // continuation request to issue and the progress snapshot already reports 100 percent.
        if (WalkProgress.IsComplete)
        {
            return OperationResult<SearchResponse>.Success(CurrentPage);
        }

        // Continue one page at a time so the current cursor is the only cursor used for the next
        // request and the API client's shared throttle can serialize every request start.
        while (CanFetchNextPage)
        {
            var result = await FetchNextAsync(cancellationToken).ConfigureAwait(false);
            if (result.Status == OperationStatus.Failure)
            {
                // Return the original operation message so the pager can report the precise failure
                // while all pages committed before this request remain available for retry.
                return result;
            }

            progress?.Invoke(WalkProgress);

            // The loop can stop as soon as the aggregate reaches the service total, even if the
            // service response also supplied a cursor for a later request.
            if (WalkProgress.IsComplete)
            {
                return result;
            }
        }

        // A cursor/cardinality stop before the stable total means the service did not provide enough
        // evidence for a complete walk. Return a failure instead of claiming that data was loaded.
        return failure(
            "isearch.paging.incomplete",
            "iSearch stopped page continuation before all result records were loaded.");

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a bounded progress snapshot from the committed session state.</summary>
    /// <returns>The current immutable progress values.</returns>
    /// <remarks>The calculation is record-based so a short final page cannot be presented as complete too early.</remarks>
    /// <seealso cref="SearchPageWalkProgress"/>
    private SearchPageWalkProgress createProgress()
    {
        #region implementation

        var isComplete = _totalResults == 0 || _walkedResults.Count >= _totalResults;
        var percentage = _totalResults == 0
            ? 100D
            : Math.Clamp((double)_walkedResults.Count / _totalResults * 100D, 0D, 100D);

        return new SearchPageWalkProgress
        {
            DataPage = CurrentPage.Cardinality.PageNumber,
            TotalDataPages = CurrentPage.Cardinality.TotalPages,
            LoadedRecords = _walkedResults.Count,
            TotalRecords = _totalResults,
            Percentage = percentage,
            IsComplete = isComplete
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one expected paging failure with the repository's standard message shape.</summary>
    /// <param name="code">The stable diagnostic code.</param>
    /// <param name="message">The safe operator-facing message.</param>
    /// <returns>A failed search-page operation containing one error message.</returns>
    /// <remarks>This helper keeps all expected walk failures consistent for the pager and diagnostics.</remarks>
    private static OperationResult<SearchResponse> failure(string code, string message)
    {
        #region implementation

        return OperationResult<SearchResponse>.Failure(
        [new OperationMessage
        {
            Code = code,
            Message = message,
            Severity = OperationMessageSeverity.Error
        }]);

        #endregion
    }

    #endregion
}
