using Carrot.Cli.CarrotApi.Contracts;
using Carrot.Cli.Common;
using Microsoft.Extensions.Logging;

namespace Carrot.Cli.CarrotApi;

/**************************************************************/
/// <summary>
/// Defines HTTP serialization, transient retry, and error translation for Carrot 4.8.6.
/// </summary>
/// <remarks>
/// Future retries are limited to transient stateless failures and never retry HTTP 400.
/// </remarks>
/// <seealso cref="ICarrotApiClient"/>
internal sealed class CarrotApiClient : ICarrotApiClient
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Initializes the client with injected HTTP and structured logging dependencies.
    /// </summary>
    /// <param name="httpClient">The host-managed HTTP client.</param>
    /// <param name="logger">The structured API-client logger.</param>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    internal CarrotApiClient(HttpClient httpClient, ILogger<CarrotApiClient> logger)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Retrieves and deserializes the exact service configuration from <c>/list</c>.
    /// </summary>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    public Task<OperationResult<ListResponse>> GetConfigurationAsync(
        Uri serviceEndpoint,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Serializes and submits one complete ordered request to <c>/cluster</c>.
    /// </summary>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    public Task<OperationResult<ClusterResponse>> ClusterAsync(
        Uri serviceEndpoint,
        ClusterRequest request,
        string? template,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
