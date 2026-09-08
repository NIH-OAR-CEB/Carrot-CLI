using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Carrot.Cli.ISearch.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Carrot.Cli.ISearch;

/**************************************************************/
/// <summary>Implements the authenticated iSearch HTTP boundary for health, discovery, and search.</summary>
/// <remarks>
/// The client sends the API key only as a cookie to the fixed HTTPS host, refuses redirects, and
/// keeps a shared timeout and bounded retry policy for each operation. Error messages never include
/// credentials, headers, or unbounded server response bodies.
/// </remarks>
/// <seealso cref="IISearchApiClient"/>
/// <seealso cref="ISearchOptions"/>
internal sealed class ISearchApiClient : IISearchApiClient
{
    #region implementation

    private const string BaseAddressText = "https://isearch.opa-tools.od.nih.gov/api/";
    private const int MaximumRows = 100;
    private readonly HttpClient _httpClient;
    private readonly ISearchOptions _options;
    private readonly ILogger<ISearchApiClient> _logger;
    private readonly SemaphoreSlim _requestGate = new(1, 1);
    private long _lastRequestTimestamp;

    /**************************************************************/
    /// <summary>Initializes the iSearch client with host-managed HTTP and optional settings.</summary>
    /// <param name="httpClient">The typed HTTP client configured with the iSearch base URI.</param>
    /// <param name="options">The optional iSearch credentials and safety settings.</param>
    /// <param name="logger">The structured logger used only for bounded retry diagnostics.</param>
    /// <exception cref="ArgumentNullException">Thrown when a required dependency is <see langword="null"/>.</exception>
    public ISearchApiClient(
        HttpClient httpClient,
        IOptions<ISearchOptions> options,
        ILogger<ISearchApiClient> logger)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _httpClient.BaseAddress ??= new Uri(BaseAddressText, UriKind.Absolute);
        _options = options.Value;
        _logger = logger;

        #endregion
    }

    /**************************************************************/
    /// <summary>Calls <c>GET /health</c> and retains the complete safe availability payload.</summary>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The tolerant health response or an expected operation failure.</returns>
    /// <remarks>Missing credentials fail locally; caller cancellation is propagated rather than converted to a success.</remarks>
    public Task<OperationResult<SearchHealthResponse>> GetHealthAsync(CancellationToken cancellationToken)
    {
        #region implementation

        return sendAsync(
            "health",
            "health",
            HttpMethod.Get,
            contentFactory: null,
            parseResponseAsync: parseHealthAsync,
            cancellationToken);

        #endregion
    }

    /**************************************************************/
    /// <summary>Calls <c>GET /datasets</c> and validates its string-array response.</summary>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The exact non-null dataset names or an expected operation failure.</returns>
    /// <remarks>The response is the sole source of dataset choices presented by the interactive flow.</remarks>
    public Task<OperationResult<IReadOnlyList<string>>> GetDatasetsAsync(CancellationToken cancellationToken)
    {
        #region implementation

        return sendAsync(
            "datasets",
            "datasets",
            HttpMethod.Get,
            contentFactory: null,
            parseResponseAsync: parseDatasetsAsync,
            cancellationToken);

        #endregion
    }

    /**************************************************************/
    /// <summary>Calls the dataset-scoped <c>GET /search/{dataset}</c> endpoint with a bounded query.</summary>
    /// <param name="request">The query request to validate and submit.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The validated search envelope or an expected operation failure.</returns>
    /// <remarks>Invalid query controls fail before request creation, and transient failures are retried only within the configured budget.</remarks>
    public Task<OperationResult<SearchResponse>> SearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Dataset)
            || string.IsNullOrWhiteSpace(request.Query)
            || !string.Equals(request.DefaultOp, "AND", StringComparison.Ordinal)
            || request.Rows is < 1 or > MaximumRows)
        {
            return Task.FromResult(failure<SearchResponse>(
                "isearch.request.invalid",
                "The iSearch request must contain a dataset, a query, defaultOp AND, and 1-100 rows."));
        }

        // The live dataset-scoped GET operation currently succeeds for grants while the body-based POST operation returns HTTP 500.
        var requestUri = $"search/{Uri.EscapeDataString(request.Dataset)}"
            + $"?q={Uri.EscapeDataString(request.Query)}"
            + $"&defaultOp={Uri.EscapeDataString(request.DefaultOp)}"
            + $"&rows={request.Rows}";

        return sendAsync(
            "search",
            requestUri,
            HttpMethod.Get,
            contentFactory: null,
            parseSearchAsync,
            cancellationToken);

        #endregion
    }

    /**************************************************************/
    /// <summary>Executes one operation with credential checks, pacing, bounded retries, and safe parsing.</summary>
    /// <typeparam name="TResponse">The successful response type.</typeparam>
    /// <param name="operation">The relative endpoint operation name.</param>
    /// <param name="requestUri">The relative request URI, including any encoded query parameters.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="contentFactory">The optional fresh request-content factory.</param>
    /// <param name="parseResponseAsync">The successful-response parser.</param>
    /// <param name="cancellationToken">The caller-owned cancellation token.</param>
    /// <returns>The parsed response or a safe operation failure.</returns>
    private async Task<OperationResult<TResponse>> sendAsync<TResponse>(
        string operation,
        string requestUri,
        HttpMethod method,
        Func<HttpContent>? contentFactory,
        Func<HttpResponseMessage, CancellationToken, Task<OperationResult<TResponse>>> parseResponseAsync,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        #region implementation

        // Keep the optional feature dormant when credentials are absent; no authenticated request is constructed on this path.
        var credentialResult = validateCredentials();
        if (credentialResult.Status == OperationStatus.Failure)
        {
            return OperationResult<TResponse>.Failure(credentialResult.Messages);
        }

        // Reject invalid safety settings locally so a malformed configuration cannot create an unbounded operation.
        if (_options.TimeoutSeconds <= 0)
        {
            return failure<TResponse>("isearch.configuration.timeout-invalid", "iSearch timeout must be greater than zero seconds.");
        }

        if (_options.TransientRetryCount < 0
            || _options.MinimumRequestIntervalMilliseconds < 0
            || _options.MaximumResponseCharacters <= 0)
        {
            return failure<TResponse>("isearch.configuration.invalid", "iSearch retry, pacing, and response-limit settings are invalid.");
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        var operationToken = timeoutSource.Token;

        // Each retry receives a fresh request because HttpRequestMessage and its content are single-use resources.
        for (var attempt = 0; attempt <= _options.TransientRetryCount; attempt++)
        {
            try
            {
                await waitForRequestSlotAsync(operationToken).ConfigureAwait(false);
                using var request = createRequest(requestUri, method, contentFactory);
                using var response = await _httpClient
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, operationToken)
                    .ConfigureAwait(false);

                // A redirect could move the authenticated cookie to a host outside the fixed iSearch service.
                if (isRedirect(response.StatusCode))
                {
                    return failure<TResponse>("isearch.redirect", $"iSearch returned redirect status {(int)response.StatusCode}; redirects are disabled.");
                }

                if (response.IsSuccessStatusCode)
                {
                    return await parseResponseAsync(response, operationToken).ConfigureAwait(false);
                }

                // Retry only statuses that are explicitly safe to repeat without changing operator input.
                if (isTransient(response.StatusCode) && attempt < _options.TransientRetryCount)
                {
                    var retryDelay = getRetryDelay(response, attempt);
                    _logger.LogWarning(
                        "iSearch {Operation} returned HTTP {StatusCode}; retrying attempt {AttemptNumber} after {RetryDelayMilliseconds}ms.",
                        operation,
                        (int)response.StatusCode,
                        attempt + 1,
                        (int)retryDelay.TotalMilliseconds);
                    await Task.Delay(retryDelay, operationToken).ConfigureAwait(false);
                    continue;
                }

                var responseBody = await readFailureBodyAsync(response, operationToken).ConfigureAwait(false);
                var bodySuffix = string.IsNullOrWhiteSpace(responseBody)
                    ? string.Empty
                    : $" Response: {responseBody}";
                return failure<TResponse>(
                    "isearch.http",
                    $"iSearch {operation} returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase ?? "Unknown Status"}).{bodySuffix}");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
            {
                return failure<TResponse>("isearch.timeout", $"The iSearch {operation} operation exceeded its configured timeout.");
            }
            catch (HttpRequestException exception) when (attempt < _options.TransientRetryCount)
            {
                _logger.LogWarning(
                    "iSearch {Operation} transport attempt {AttemptNumber} failed and will be retried: {Message}",
                    operation,
                    attempt + 1,
                    exception.Message);
                await Task.Delay(getRetryDelay(null, attempt), operationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException exception)
            {
                return failure<TResponse>("isearch.transport", $"Could not contact iSearch for {operation}: {exception.Message}");
            }
            catch (JsonException exception)
            {
                return failure<TResponse>("isearch.response.malformed", $"iSearch returned malformed JSON for {operation}: {exception.Message}");
            }
        }

        return failure<TResponse>("isearch.transport", $"The iSearch {operation} operation exhausted all attempts.");

        #endregion
    }

    /**************************************************************/
    /// <summary>Validates credentials before any request or request object is constructed.</summary>
    /// <returns>A success result or safe missing-configuration messages.</returns>
    private OperationResult<bool> validateCredentials()
    {
        #region implementation

        var messages = new List<OperationMessage>();
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            messages.Add(new OperationMessage
            {
                Code = "isearch.configuration.api-key-missing",
                Message = "iSearch API key is not configured.",
                Severity = OperationMessageSeverity.Error
            });
        }

        if (string.IsNullOrWhiteSpace(_options.ContactEmail))
        {
            messages.Add(new OperationMessage
            {
                Code = "isearch.configuration.contact-email-missing",
                Message = "iSearch contact email is not configured.",
                Severity = OperationMessageSeverity.Error
            });
        }

        return messages.Count == 0
            ? OperationResult<bool>.Success(true)
            : OperationResult<bool>.Failure(messages);

        #endregion
    }

    /**************************************************************/
    /// <summary>Waits for the shared sequential-request pace without delaying the first request.</summary>
    /// <param name="cancellationToken">The operation cancellation token.</param>
    private async Task waitForRequestSlotAsync(CancellationToken cancellationToken)
    {
        #region implementation

        // Serialize pacing state so concurrent callers cannot bypass the minimum interval.
        await _requestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var last = Interlocked.Read(ref _lastRequestTimestamp);

            // Timestamp request start rather than completion so sequential starts remain predictably spaced.
            if (last != 0 && _options.MinimumRequestIntervalMilliseconds > 0)
            {
                var elapsed = Stopwatch.GetElapsedTime(last);
                var remaining = TimeSpan.FromMilliseconds(_options.MinimumRequestIntervalMilliseconds) - elapsed;
                if (remaining > TimeSpan.Zero)
                {
                    await Task.Delay(remaining, cancellationToken).ConfigureAwait(false);
                }
            }

            Interlocked.Exchange(ref _lastRequestTimestamp, Stopwatch.GetTimestamp());
        }
        finally
        {
            _requestGate.Release();
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a fresh authenticated request for one retry attempt.</summary>
    /// <param name="requestUri">The relative request URI, including any encoded query parameters.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="contentFactory">The optional request body factory.</param>
    /// <returns>The authenticated request message.</returns>
    private HttpRequestMessage createRequest(string requestUri, HttpMethod method, Func<HttpContent>? contentFactory)
    {
        #region implementation

        // Keep the secret in a cookie header; relative request paths keep the credential off the URI.
        var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("Cookie", $"apiKey={_options.ApiKey}");
        request.Headers.TryAddWithoutValidation("From", _options.ContactEmail);
        if (contentFactory is not null)
        {
            request.Content = contentFactory();
        }

        return request;

        #endregion
    }

    /**************************************************************/
    /// <summary>Reads a bounded non-success response body for operator diagnostics.</summary>
    /// <param name="response">The failed HTTP response.</param>
    /// <param name="cancellationToken">The operation cancellation token.</param>
    /// <returns>The trimmed response body, or an empty string when no body was supplied.</returns>
    /// <remarks>The configured character limit prevents an upstream failure from flooding terminal output or logs.</remarks>
    private async Task<string> readFailureBodyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        #region implementation

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (body.Length > _options.MaximumResponseCharacters)
        {
            body = body[.._options.MaximumResponseCharacters];
        }

        return body.Trim();

        #endregion
    }

    /**************************************************************/
    /// <summary>Parses a tolerant health object and keeps its complete JSON payload for display.</summary>
    /// <param name="response">The successful HTTP response.</param>
    /// <param name="cancellationToken">The operation cancellation token.</param>
    /// <returns>The health response or a malformed-response failure.</returns>
    private async Task<OperationResult<SearchHealthResponse>> parseHealthAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        #region implementation

        using var document = await readJsonAsync(
            response,
            cancellationToken,
            _options.MaximumResponseCharacters).ConfigureAwait(false);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return failure<SearchHealthResponse>("isearch.response.malformed", "iSearch health returned a non-object JSON payload.");
        }

        var status = document.RootElement.TryGetProperty("status", out var statusElement)
            && statusElement.ValueKind == JsonValueKind.String
            ? statusElement.GetString()
            : null;
        return OperationResult<SearchHealthResponse>.Success(new SearchHealthResponse
        {
            Status = status,
            Payload = document.RootElement.Clone()
        });

        #endregion
    }

    /**************************************************************/
    /// <summary>Parses the required string-array dataset response.</summary>
    /// <param name="response">The successful HTTP response.</param>
    /// <param name="cancellationToken">The operation cancellation token.</param>
    /// <returns>The exact dataset names or a malformed-response failure.</returns>
    private async Task<OperationResult<IReadOnlyList<string>>> parseDatasetsAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        #region implementation

        using var document = await readJsonAsync(
            response,
            cancellationToken,
            _options.MaximumResponseCharacters).ConfigureAwait(false);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return failure<IReadOnlyList<string>>("isearch.response.malformed", "iSearch datasets returned a non-array JSON payload.");
        }

        var datasets = new List<string>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(element.GetString()))
            {
                return failure<IReadOnlyList<string>>("isearch.response.malformed", "iSearch datasets must contain only nonempty strings.");
            }

            datasets.Add(element.GetString()!);
        }

        return OperationResult<IReadOnlyList<string>>.Success(datasets);

        #endregion
    }

    /**************************************************************/
    /// <summary>Parses and validates the documented search envelope.</summary>
    /// <param name="response">The successful HTTP response.</param>
    /// <param name="cancellationToken">The operation cancellation token.</param>
    /// <returns>The search envelope or a malformed-response failure.</returns>
    private static async Task<OperationResult<SearchResponse>> parseSearchAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        #region implementation

        using var document = await readJsonAsync(response, cancellationToken).ConfigureAwait(false);
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("returnedCount", out var returnedCount)
            || !document.RootElement.TryGetProperty("totalCount", out var totalCount)
            || !document.RootElement.TryGetProperty("results", out var results)
            || !returnedCount.TryGetInt32(out var returned)
            || !totalCount.TryGetInt32(out var total)
            || results.ValueKind != JsonValueKind.Array)
        {
            return failure<SearchResponse>("isearch.response.malformed", "iSearch search returned an incomplete response envelope.");
        }

        // Clone elements before disposing the document so generic records remain valid after parsing returns.
        var responseValue = new SearchResponse
        {
            ReturnedCount = returned,
            TotalCount = total,
            Results = results.EnumerateArray().Select(item => item.Clone()).ToArray(),
            Cursor = document.RootElement.TryGetProperty("cursor", out var cursor)
                && cursor.ValueKind == JsonValueKind.String
                ? cursor.GetString()
                : null
        };

        return responseValue.ReturnedCount == responseValue.Results.Count
            ? OperationResult<SearchResponse>.Success(responseValue)
            : failure<SearchResponse>("isearch.response.malformed", "iSearch search returned a count that does not match its records.");

        #endregion
    }

    /**************************************************************/
    /// <summary>Reads a successful JSON body while retaining only bounded diagnostic capacity.</summary>
    /// <param name="response">The HTTP response whose body is read.</param>
    /// <param name="cancellationToken">The operation cancellation token.</param>
    /// <param name="maximumCharacters">The optional character limit, where zero means unlimited.</param>
    /// <returns>A parsed JSON document.</returns>
    private static async Task<JsonDocument> readJsonAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken,
        int maximumCharacters = 0)
    {
        #region implementation

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (maximumCharacters > 0 && body.Length > maximumCharacters)
        {
            throw new JsonException("The response exceeded the configured display limit.");
        }

        return JsonDocument.Parse(body);

        #endregion
    }

    /**************************************************************/
    /// <summary>Calculates a bounded retry delay, honoring a valid server Retry-After value.</summary>
    /// <param name="response">The transient response, or null for a transport failure.</param>
    /// <param name="attempt">The zero-based failed attempt number.</param>
    /// <returns>The retry delay.</returns>
    private static TimeSpan getRetryDelay(HttpResponseMessage? response, int attempt)
    {
        #region implementation

        if (response?.Headers.RetryAfter?.Delta is { } delta && delta >= TimeSpan.Zero)
        {
            return TimeSpan.FromSeconds(Math.Min(delta.TotalSeconds, 30));
        }

        if (response?.Headers.RetryAfter?.Date is { } date)
        {
            var delay = date - DateTimeOffset.UtcNow;
            if (delay >= TimeSpan.Zero)
            {
                return TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds, 30));
            }
        }

        return TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attempt)));

        #endregion
    }

    /**************************************************************/
    /// <summary>Identifies statuses that may be retried without changing the request.</summary>
    /// <param name="statusCode">The HTTP status.</param>
    /// <returns><see langword="true"/> for 408, 429, and server failures.</returns>
    private static bool isTransient(HttpStatusCode statusCode)
    {
        #region implementation

        return statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
            || (int)statusCode >= 500;

        #endregion
    }

    /**************************************************************/
    /// <summary>Identifies redirects that must be rejected rather than followed.</summary>
    /// <param name="statusCode">The HTTP status.</param>
    /// <returns><see langword="true"/> for the 3xx family.</returns>
    private static bool isRedirect(HttpStatusCode statusCode)
    {
        #region implementation

        return (int)statusCode is >= 300 and <= 399;

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one safe typed failure message.</summary>
    /// <typeparam name="TResponse">The expected response type.</typeparam>
    /// <param name="code">The stable diagnostic code.</param>
    /// <param name="message">The bounded user-facing message.</param>
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
