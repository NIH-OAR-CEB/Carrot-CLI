using Carrot.Cli.Common;
using Carrot.Cli.ISearch.Contracts;

namespace Carrot.Cli.ISearch;

/**************************************************************/
/// <summary>Defines the authenticated iSearch operations used by the interactive workflow.</summary>
/// <remarks>
/// The boundary owns credentials, HTTP safety, response validation, retry policy, and cancellation.
/// Callers receive safe operation messages and never need to construct an authenticated request.
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
    /// <param name="request">The selected dataset and nonempty query controls.</param>
    /// <param name="cancellationToken">The token that cancels the request.</param>
    /// <returns>The validated count and generic JSON records for the selected result fields, or a safe failure.</returns>
    /// <seealso cref="SearchRequest"/>
    /// <seealso cref="SearchResponse"/>
    Task<OperationResult<SearchResponse>> SearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken);
}
