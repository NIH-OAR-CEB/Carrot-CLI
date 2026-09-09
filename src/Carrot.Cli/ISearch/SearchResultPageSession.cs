using System.Text.Json;
using Carrot.Cli.Common;
using Carrot.Cli.ISearch.Contracts;

namespace Carrot.Cli.ISearch;

/**************************************************************/
/// <summary>Maintains one bounded iSearch result-page transition sequence.</summary>
/// <remarks>
/// The session keeps the original query context and latest validated response together so an
/// interactive pager and a future crawler can advance with the same cursor and page invariants.
/// It performs at most one network transition per call and does not accumulate or persist records.
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

    /**************************************************************/
    /// <summary>Initializes a page session from a stable query and its first successful response.</summary>
    /// <param name="client">The authenticated iSearch API boundary used for continuation requests.</param>
    /// <param name="request">The unchanged database, query, fields, operator, and row-limit context.</param>
    /// <param name="initialPage">The validated first response to expose as the current page.</param>
    /// <param name="returnDataset">The configured return-dataset label, when the interactive flow supplies one.</param>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
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

        #endregion
    }

    /**************************************************************/
    /// <summary>Gets the latest successfully validated service response.</summary>
    /// <remarks>The value changes only after a successful continuation response has been accepted.</remarks>
    public SearchResponse CurrentPage { get; private set; }

    /**************************************************************/
    /// <summary>Gets the unchanged request context used for every walked result page.</summary>
    public SearchRequest Request => _request;

    /**************************************************************/
    /// <summary>Gets every successfully walked service page in result-page order.</summary>
    /// <remarks>The collection includes the initial response and is retained for explicit export.</remarks>
    public IReadOnlyList<SearchResponse> WalkedPages => _walkedPages.AsReadOnly();

    /**************************************************************/
    /// <summary>Gets every record from every successfully walked page in service order.</summary>
    /// <remarks>Records are preserved exactly as returned, including duplicate records across pages.</remarks>
    public IReadOnlyList<JsonElement> WalkedResults => _walkedResults.AsReadOnly();

    /**************************************************************/
    /// <summary>Gets the exact live dataset associated with the stable query context.</summary>
    public string Dataset => _request.Dataset;

    /**************************************************************/
    /// <summary>Gets the configured return-dataset label associated with this interactive result view.</summary>
    public string? ReturnDataset { get; }

    /**************************************************************/
    /// <summary>Gets whether the current service response can advance to another result page.</summary>
    /// <remarks>
    /// Both a service cursor and remaining cardinality pages are required. Terminal display-page
    /// counts are intentionally excluded because they describe only how records fit on a screen.
    /// </remarks>
    public bool CanFetchNextPage => !string.IsNullOrWhiteSpace(CurrentPage.Cursor)
        && CurrentPage.Cardinality.PageNumber > 0
        && CurrentPage.Cardinality.PageNumber < CurrentPage.Cardinality.TotalPages;

    /**************************************************************/
    /// <summary>Fetches and adopts exactly one subsequent service result page.</summary>
    /// <param name="cancellationToken">The token that cancels the continuation request.</param>
    /// <returns>The new current page on success or a safe operation failure.</returns>
    /// <remarks>
    /// A failed request leaves <see cref="CurrentPage"/> unchanged, allowing an interactive user or
    /// future crawler to retry without losing the last validated record chunk.
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

            // Append before exposing the new current page so the session's page and aggregate state
            // advance as one transition; a failed operation never changes either collection.
            _walkedPages.Add(nextPage);
            _walkedResults.AddRange(nextPage.Results);
            CurrentPage = nextPage;
        }

        return result;

        #endregion
    }

    #endregion
}
