using Carrot.Cli.Common;
using Carrot.Cli.ISearch.Contracts;

namespace Carrot.Cli.ISearch;

/**************************************************************/
/// <summary>Defines the authenticated iSearch operations used by the interactive workflow.</summary>
/// <remarks>
/// The boundary owns credentials, HTTP safety, response validation, retry policy, cancellation, and
/// one-page cursor continuation. Callers receive safe operation messages and never construct an
/// authenticated request themselves.
/// </remarks>
/// <seealso cref="ISearchApiClient"/>
/// <seealso cref="SearchRequest"/>
/// <seealso cref="SearchResponse"/>
internal interface IISearchApiClient
{
    /**************************************************************/
    /// <summary>Retrieves the service availability payload from <c>GET /health</c>.</summary>
    /// <param name="cancellationToken">The token that cancels the request.</param>
    /// <returns>The parsed health response or a safe failure.</returns>
    /// <seealso cref="SearchHealthResponse"/>
    Task<OperationResult<SearchHealthResponse>> GetHealthAsync(CancellationToken cancellationToken);

    /**************************************************************/
    /// <summary>Retrieves the live selectable dataset names from <c>GET /datasets</c>.</summary>
    /// <param name="cancellationToken">The token that cancels the request.</param>
    /// <returns>The exact dataset names returned by iSearch or a safe failure.</returns>
    Task<OperationResult<IReadOnlyList<string>>> GetDatasetsAsync(CancellationToken cancellationToken);

    /**************************************************************/
    /// <summary>Retrieves field metadata from <c>GET /fields/{dataset}</c>.</summary>
    /// <param name="dataset">The nonempty dataset name whose field schema is requested.</param>
    /// <param name="cancellationToken">The token that cancels the request.</param>
    /// <returns>The validated field definitions or a safe failure.</returns>
    /// <seealso cref="SearchField"/>
    Task<OperationResult<IReadOnlyList<SearchField>>> GetFieldsAsync(
        string dataset,
        CancellationToken cancellationToken);

    /**************************************************************/
    /// <summary>Submits one bounded search request to <c>GET /search/{dataset}</c>.</summary>
    /// <param name="request">The selected dataset, nonempty query controls, and ordered result fields.</param>
    /// <param name="cancellationToken">The token that cancels the request.</param>
    /// <returns>The validated generic records, cursor, and nested cardinality for the selected result fields, or a safe failure.</returns>
    /// <seealso cref="SearchRequest"/>
    /// <seealso cref="SearchResponse"/>
    Task<OperationResult<SearchResponse>> SearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken);

    /**************************************************************/
    /// <summary>Fetches exactly one subsequent result page using the service cursor.</summary>
    /// <param name="request">The unchanged database, query, operator, fields, and row-limit context.</param>
    /// <param name="cursor">The nonempty continuation cursor returned by the preceding result page.</param>
    /// <param name="nextPageNumber">The one-based result-page number expected from this request.</param>
    /// <param name="cancellationToken">The token that cancels the request.</param>
    /// <returns>The validated next result page or a safe operation failure.</returns>
    /// <remarks>
    /// This operation never loops or accumulates pages. Callers such as the interactive page session
    /// and a future bounded crawler control how many times it is invoked.
    /// </remarks>
    /// <seealso cref="SearchRequest"/>
    /// <seealso cref="SearchResponse"/>
    Task<OperationResult<SearchResponse>> SearchNextPageAsync(
        SearchRequest request,
        string cursor,
        int nextPageNumber,
        CancellationToken cancellationToken);
}
