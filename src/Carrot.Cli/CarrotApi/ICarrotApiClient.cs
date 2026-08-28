using Carrot.Cli.CarrotApi.Contracts;
using Carrot.Cli.Common;

namespace Carrot.Cli.CarrotApi;

/**************************************************************/
/// <summary>
/// Defines the Carrot 4.8.6 list and cluster HTTP contract boundary.
/// </summary>
/// <seealso cref="CarrotApiClient"/>
internal interface ICarrotApiClient
{
    /**************************************************************/
    /// <summary>
    /// Retrieves algorithms, supported languages, and named templates from <c>/list</c>.
    /// </summary>
    /// <param name="serviceEndpoint">The absolute service base URI ending at <c>/service</c>.</param>
    /// <param name="timeout">The request timeout for this operation.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>A task containing the exact list response or structured endpoint failures.</returns>
    Task<OperationResult<ListResponse>> GetConfigurationAsync(
        Uri serviceEndpoint,
        TimeSpan timeout,
        CancellationToken cancellationToken);

    /**************************************************************/
    /// <summary>
    /// Submits all valid documents in one ordered request to <c>/cluster</c>.
    /// </summary>
    /// <param name="serviceEndpoint">The absolute service base URI ending at <c>/service</c>.</param>
    /// <param name="request">The OpenAPI-aligned cluster request body.</param>
    /// <param name="template">The optional named template query parameter.</param>
    /// <param name="timeout">The request timeout for this operation.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>A task containing the exact cluster response or structured request failures.</returns>
    Task<OperationResult<ClusterResponse>> ClusterAsync(
        Uri serviceEndpoint,
        ClusterRequest request,
        string? template,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
