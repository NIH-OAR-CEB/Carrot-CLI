using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Carrot.Cli.CarrotApi.Contracts;
using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Carrot.Cli.CarrotApi;

/**************************************************************/
/// <summary>
/// Defines HTTP serialization, transient retry, and error translation for Carrot 4.8.6.
/// </summary>
/// <remarks>
/// Retries are limited to transient stateless failures and never retry HTTP 400, redirects,
/// malformed JSON, caller cancellation, or response-contract failures. Each public operation's
/// timeout is one shared budget across all attempts rather than a fresh budget per attempt.
/// </remarks>
/// <seealso cref="ICarrotApiClient"/>
internal sealed class CarrotApiClient : ICarrotApiClient
{
    #region implementation

    private static readonly JsonSerializerOptions SerializerOptions = new();

    private readonly HttpClient _httpClient;
    private readonly ILogger<CarrotApiClient> _logger;
    private readonly int _retryCount;

    /**************************************************************/
    /// <summary>
    /// Initializes the client with injected HTTP and structured logging dependencies.
    /// </summary>
    /// <param name="httpClient">The host-managed HTTP client.</param>
    /// <param name="logger">The structured API-client logger.</param>
    /// <param name="options">The validated retry configuration.</param>
    public CarrotApiClient(
        HttpClient httpClient,
        ILogger<CarrotApiClient> logger,
        IOptions<CarrotCliOptions> options)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);
        _httpClient = httpClient;
        _logger = logger;
        _retryCount = options.Value.TransientRetryCount;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Retrieves and deserializes the exact service configuration from <c>/list</c>.
    /// </summary>
    public Task<OperationResult<ListResponse>> GetConfigurationAsync(
        Uri serviceEndpoint,
        TimeSpan timeout,
        bool? indent,
        CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(serviceEndpoint);
        return sendAsync<ListResponse>(
            () => createRequest(HttpMethod.Get, createOperationUri(serviceEndpoint, "list", null, indent)),
            timeout,
            cancellationToken);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Serializes and submits one complete ordered request to <c>/cluster</c>.
    /// </summary>
    public Task<OperationResult<ClusterResponse>> ClusterAsync(
        Uri serviceEndpoint,
        ClusterRequest request,
        string? template,
        TimeSpan timeout,
        bool? indent,
        CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(serviceEndpoint);
        ArgumentNullException.ThrowIfNull(request);
        return sendAsync<ClusterResponse>(
            () =>
            {
                var message = createRequest(
                    HttpMethod.Post,
                    createOperationUri(serviceEndpoint, "cluster", template, indent));
                var json = JsonSerializer.Serialize(request, SerializerOptions);
                message.Content = new StringContent(json, Encoding.UTF8, "application/json");
                return message;
            },
            timeout,
            cancellationToken);

        #endregion
    }

    /**************************************************************/
    /// <summary>Executes one JSON operation with a single timeout budget and bounded transient retries.</summary>
    /// <typeparam name="TResponse">The expected successful response contract.</typeparam>
    /// <param name="requestFactory">Creates a fresh request for each send attempt.</param>
    /// <param name="timeout">The complete time budget shared by every attempt.</param>
    /// <param name="cancellationToken">The caller-owned cancellation token.</param>
    /// <returns>The parsed response or a structured transport, HTTP, timeout, or contract failure.</returns>
    private async Task<OperationResult<TResponse>> sendAsync<TResponse>(
        Func<HttpRequestMessage> requestFactory,
        TimeSpan timeout,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        #region implementation

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "The operation timeout must be greater than zero.");
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        var operationToken = timeoutSource.Token;

        for (var attempt = 0; attempt <= _retryCount; attempt++)
        {
            try
            {
                using var request = requestFactory();
                using var response = await _httpClient
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, operationToken)
                    .ConfigureAwait(false);

                if (isRedirect(response.StatusCode))
                {
                    return failure<TResponse>(
                        "carrot.redirect",
                        $"Carrot returned redirect status {(int)response.StatusCode}; redirects are disabled for document safety.");
                }

                if (response.IsSuccessStatusCode)
                {
                    return await deserializeSuccessAsync<TResponse>(response, operationToken).ConfigureAwait(false);
                }

                if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.InternalServerError)
                {
                    var errorResult = await deserializeCarrotErrorAsync<TResponse>(response, operationToken)
                        .ConfigureAwait(false);
                    if (response.StatusCode == HttpStatusCode.BadRequest
                        || errorResult.Messages.Any(message => message.Code == "carrot.response.malformed")
                        || attempt == _retryCount)
                    {
                        return errorResult;
                    }

                    logRetry(response.StatusCode, attempt + 1);
                    continue;
                }

                if (isTransient(response.StatusCode) && attempt < _retryCount)
                {
                    logRetry(response.StatusCode, attempt + 1);
                    continue;
                }

                return failure<TResponse>(
                    "carrot.http",
                    $"Carrot returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase ?? "Unknown Status"}).");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
            {
                return failure<TResponse>(
                    "carrot.timeout",
                    $"The Carrot operation exceeded its overall {timeout.TotalSeconds:0.###}-second timeout.");
            }
            catch (HttpRequestException exception) when (attempt < _retryCount)
            {
                _logger.LogWarning(
                    "Carrot transport attempt {AttemptNumber} failed and will be retried: {Message}",
                    attempt + 1,
                    exception.Message);
            }
            catch (HttpRequestException exception)
            {
                return failure<TResponse>("carrot.transport", $"Could not contact Carrot: {exception.Message}");
            }
        }

        return failure<TResponse>("carrot.transport", "The Carrot operation exhausted all attempts.");

        #endregion
    }

    /**************************************************************/
    /// <summary>Deserializes a successful JSON response without retrying malformed content.</summary>
    /// <typeparam name="TResponse">The expected successful response contract.</typeparam>
    /// <param name="response">The successful HTTP response.</param>
    /// <param name="cancellationToken">The overall operation token.</param>
    /// <returns>The parsed response or a malformed-response failure.</returns>
    private static async Task<OperationResult<TResponse>> deserializeSuccessAsync<TResponse>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        #region implementation

        try
        {
            await using var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var value = await JsonSerializer.DeserializeAsync<TResponse>(
                content,
                SerializerOptions,
                cancellationToken).ConfigureAwait(false);
            if (value is null)
            {
                return failure<TResponse>("carrot.response.malformed", "Carrot returned an empty JSON response.");
            }

            return hasValidContract(value)
                ? OperationResult<TResponse>.Success(value)
                : failure<TResponse>(
                    "carrot.response.malformed",
                    "Carrot returned JSON that does not satisfy the documented response contract.");
        }
        catch (JsonException exception)
        {
            return failure<TResponse>(
                "carrot.response.malformed",
                $"Carrot returned malformed JSON: {exception.Message}");
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Translates a documented HTTP 400 or 500 body without exposing server stack details.</summary>
    /// <typeparam name="TResponse">The successful operation response type.</typeparam>
    /// <param name="response">The documented Carrot error response.</param>
    /// <param name="cancellationToken">The overall operation token.</param>
    /// <returns>A structured failure containing only the documented type and message.</returns>
    private static async Task<OperationResult<TResponse>> deserializeCarrotErrorAsync<TResponse>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        #region implementation

        try
        {
            await using var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var errorResponse = await JsonSerializer.DeserializeAsync<CarrotErrorResponse>(
                content,
                SerializerOptions,
                cancellationToken).ConfigureAwait(false);
            return errorResponse is null || errorResponse.Message is null
                ? failure<TResponse>("carrot.response.malformed", "Carrot returned an empty error response.")
                : failure<TResponse>(
                    "carrot.error",
                    $"Carrot {formatErrorType(errorResponse.Type)}: {errorResponse.Message}");
        }
        catch (JsonException exception)
        {
            return failure<TResponse>(
                "carrot.response.malformed",
                $"Carrot returned a malformed error response: {exception.Message}");
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Validates non-null collections that JSON required-member checks cannot protect from explicit nulls.</summary>
    /// <typeparam name="TResponse">The deserialized response contract type.</typeparam>
    /// <param name="response">The deserialized response.</param>
    /// <returns><see langword="true"/> when required collections and nested nodes are usable.</returns>
    private static bool hasValidContract<TResponse>(TResponse response)
        where TResponse : class
    {
        #region implementation

        return response switch
        {
            ListResponse listResponse => listResponse.Algorithms is not null
                && listResponse.Templates is not null
                && listResponse.Algorithms.All(pair => pair.Value is not null),
            ClusterResponse clusterResponse => hasValidClusterNodes(clusterResponse.Clusters),
            _ => true
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>Recursively validates explicitly nullable collections in a cluster response.</summary>
    /// <param name="nodes">The current cluster-node collection.</param>
    /// <returns><see langword="true"/> when every node and collection is non-null.</returns>
    private static bool hasValidClusterNodes(IReadOnlyList<ClusterNode>? nodes)
    {
        #region implementation

        if (nodes is null)
        {
            return false;
        }

        foreach (var node in nodes)
        {
            if (node is null
                || node.Labels is null
                || node.Documents is null
                || !hasValidClusterNodes(node.Clusters))
            {
                return false;
            }
        }

        return true;

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates an HTTP request with JSON response negotiation.</summary>
    /// <param name="method">The HTTP operation method.</param>
    /// <param name="uri">The complete operation URI.</param>
    /// <returns>A disposable request message.</returns>
    private static HttpRequestMessage createRequest(HttpMethod method, Uri uri)
    {
        #region implementation

        var request = new HttpRequestMessage(method, uri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;

        #endregion
    }

    /**************************************************************/
    /// <summary>Constructs a list or cluster URI from the validated service endpoint and optional query values.</summary>
    /// <param name="serviceEndpoint">The absolute base URI ending in <c>/service</c>.</param>
    /// <param name="operation">The operation path segment.</param>
    /// <param name="template">The optional named cluster template.</param>
    /// <param name="indent">The optional JSON indentation switch.</param>
    /// <returns>The complete request URI.</returns>
    private static Uri createOperationUri(
        Uri serviceEndpoint,
        string operation,
        string? template,
        bool? indent)
    {
        #region implementation

        var queryValues = new List<string>();
        if (template is not null)
        {
            queryValues.Add($"template={Uri.EscapeDataString(template)}");
        }

        if (indent.HasValue)
        {
            queryValues.Add($"indent={indent.Value.ToString().ToLowerInvariant()}");
        }

        var baseText = serviceEndpoint.AbsoluteUri.TrimEnd('/');
        var query = queryValues.Count == 0 ? string.Empty : $"?{string.Join('&', queryValues)}";
        return new Uri($"{baseText}/{operation}{query}", UriKind.Absolute);

        #endregion
    }

    /**************************************************************/
    /// <summary>Returns whether an HTTP status is safe to retry for this stateless integration.</summary>
    /// <param name="statusCode">The returned HTTP status.</param>
    /// <returns><see langword="true"/> for 408, 429, and server errors; otherwise <see langword="false"/>.</returns>
    private static bool isTransient(HttpStatusCode statusCode)
    {
        #region implementation

        var numericStatus = (int)statusCode;
        return statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
            || numericStatus >= 500;

        #endregion
    }

    /**************************************************************/
    /// <summary>Returns whether a status is an HTTP redirect that must not be followed.</summary>
    /// <param name="statusCode">The returned HTTP status.</param>
    /// <returns><see langword="true"/> for the 300 through 399 status family.</returns>
    private static bool isRedirect(HttpStatusCode statusCode)
    {
        #region implementation

        var numericStatus = (int)statusCode;
        return numericStatus is >= 300 and <= 399;

        #endregion
    }

    /**************************************************************/
    /// <summary>Formats a documented enum name as its exact Carrot wire identifier.</summary>
    /// <param name="errorType">The parsed error type.</param>
    /// <returns>The uppercase wire identifier.</returns>
    private static string formatErrorType(CarrotErrorType errorType)
    {
        #region implementation

        return errorType switch
        {
            CarrotErrorType.BadRequest => "BAD_REQUEST",
            CarrotErrorType.Licensing => "LICENSING",
            CarrotErrorType.UnhandledError => "UNHANDLED_ERROR",
            _ => errorType.ToString()
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>Logs one bounded retry without logging document content or server stack traces.</summary>
    /// <param name="statusCode">The transient status that triggered the retry.</param>
    /// <param name="attemptNumber">The one-based failed attempt number.</param>
    private void logRetry(HttpStatusCode statusCode, int attemptNumber)
    {
        #region implementation

        _logger.LogWarning(
            "Carrot returned HTTP {StatusCode} on attempt {AttemptNumber}; retrying the stateless operation.",
            (int)statusCode,
            attemptNumber);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one immutable failed operation result.</summary>
    /// <typeparam name="TResponse">The absent response value type.</typeparam>
    /// <param name="code">The stable failure code.</param>
    /// <param name="message">The safe user-facing failure message.</param>
    /// <returns>A failed operation result.</returns>
    private static OperationResult<TResponse> failure<TResponse>(string code, string message)
    {
        #region implementation

        return OperationResult<TResponse>.Failure(
            [new OperationMessage { Code = code, Message = message, Severity = OperationMessageSeverity.Error }]);

        #endregion
    }

    #endregion
}
