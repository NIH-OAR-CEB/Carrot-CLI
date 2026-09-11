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
    /// <summary>Calls <c>GET /fields/{dataset}</c> and validates the field array response.</summary>
    /// <param name="dataset">The nonempty dataset name to encode into the request path.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The typed field definitions or an expected operation failure.</returns>
    /// <remarks>Only the required field name is enforced; optional service metadata remains nullable.</remarks>
    public Task<OperationResult<IReadOnlyList<SearchField>>> GetFieldsAsync(
        string dataset,
        CancellationToken cancellationToken)
    {
        #region implementation

        if (string.IsNullOrWhiteSpace(dataset))
        {
            // Reject the path parameter before authentication or HTTP work so callers receive a
            // deterministic request error and the service never sees an incomplete endpoint.
            return Task.FromResult(failure<IReadOnlyList<SearchField>>(
                "isearch.request.invalid",
                "The iSearch fields request must contain a dataset."));
        }

        return sendAsync(
            "fields",
            $"fields/{Uri.EscapeDataString(dataset)}",
            HttpMethod.Get,
            contentFactory: null,
            parseFieldsAsync,
            cancellationToken);

        #endregion
    }

    /**************************************************************/
    /// <summary>Calls the dataset-scoped <c>GET /search/{dataset}</c> endpoint with a bounded initial query.</summary>
    /// <param name="request">The query request to validate and submit.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The validated first search envelope or an expected operation failure.</returns>
    /// <remarks>Invalid query controls fail before request creation, and transient failures are retried only within the configured budget.</remarks>
    public Task<OperationResult<SearchResponse>> SearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken)
    {
        #region implementation

        return searchAsync(request, cursor: null, pageNumber: 1, cancellationToken);

        #endregion
    }

    /**************************************************************/
    /// <summary>Calls the dataset-scoped search endpoint for one cursor-selected result page.</summary>
    /// <param name="request">The unchanged database, query, operator, fields, and row-limit context.</param>
    /// <param name="cursor">The nonempty cursor supplied by the preceding successful response.</param>
    /// <param name="nextPageNumber">The one-based result-page number expected from the response.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The validated continuation response or an expected operation failure.</returns>
    /// <remarks>
    /// The cursor is a service continuation token, not a terminal display-page index. This method
    /// performs one request so callers can impose their own bounded walking policy.
    /// </remarks>
    /// <seealso cref="SearchAsync"/>
    public Task<OperationResult<SearchResponse>> SearchNextPageAsync(
        SearchRequest request,
        string cursor,
        int nextPageNumber,
        CancellationToken cancellationToken)
    {
        #region implementation

        if (string.IsNullOrWhiteSpace(cursor) || nextPageNumber < 1)
        {
            // Reject unusable continuation state before the credential gate and HTTP pipeline so a
            // caller cannot accidentally issue a fresh first-page request or a phantom page.
            return Task.FromResult(failure<SearchResponse>(
                "isearch.paging.invalid",
                "The iSearch next-page request must contain a cursor and a positive result-page number."));
        }

        return searchAsync(request, cursor.Trim(), nextPageNumber, cancellationToken);

        #endregion
    }

    /**************************************************************/
    /// <summary>Validates and submits one initial or cursor-based search request.</summary>
    /// <param name="request">The stable database, query, field, operator, and row-limit context.</param>
    /// <param name="cursor">The optional service cursor for a continuation request.</param>
    /// <param name="pageNumber">The one-based page context supplied to the response parser.</param>
    /// <param name="cancellationToken">The caller-owned cancellation token.</param>
    /// <returns>The validated search response or an expected operation failure.</returns>
    /// <remarks>Both entry points share this method so initial and continuation requests cannot drift in validation or safety policy.</remarks>
    /// <seealso cref="SearchAsync"/>
    /// <seealso cref="SearchNextPageAsync"/>
    private Task<OperationResult<SearchResponse>> searchAsync(
        SearchRequest request,
        string? cursor,
        int pageNumber,
        CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(request);

        // Validate the complete request contract locally. This keeps malformed dataset names,
        // empty queries, unusable fields, unsupported operators, and unsafe row counts out of HTTP.
        if (string.IsNullOrWhiteSpace(request.Dataset)
            || string.IsNullOrWhiteSpace(request.Query)
            || request.Fields is null
            || request.Fields.Count == 0
            || request.Fields.Any(string.IsNullOrWhiteSpace)
            || !string.Equals(request.DefaultOp, "AND", StringComparison.Ordinal)
                && !string.Equals(request.DefaultOp, "OR", StringComparison.Ordinal)
            || request.QueryFields?.Any(string.IsNullOrWhiteSpace) == true
            || request.FilterQueries?.Any(string.IsNullOrWhiteSpace) == true
            || !isValidDate(request.UpdatedAfter)
            || !isValidDate(request.UpdatedBefore)
            || request.Rows is < 1 or > MaximumRows
            || pageNumber < 1)
        {
            return Task.FromResult(failure<SearchResponse>(
                "isearch.request.invalid",
                "The iSearch request must contain a dataset, query, result fields, defaultOp AND or OR, valid optional controls, and 1-100 rows."));
        }

        // Keep the query context identical for every page; only the optional cursor distinguishes a
        // continuation from the initial service request.
        var requestUri = createSearchUri(request, cursor);

        return sendAsync(
            "search",
            requestUri,
            HttpMethod.Get,
            contentFactory: null,
            (response, cancellation) => parseSearchAsync(response, request.Rows, pageNumber, cancellation),
            cancellationToken);

        #endregion
    }

    /**************************************************************/
    /// <summary>Builds the encoded search URI shared by initial and continuation requests.</summary>
    /// <param name="request">The validated search query context.</param>
    /// <param name="cursor">The optional service cursor.</param>
    /// <returns>A relative dataset-scoped search URI.</returns>
    /// <remarks>The cursor is encoded as a query value and is never mixed into the selected record-field list.</remarks>
    private static string createSearchUri(SearchRequest request, string? cursor)
    {
        #region implementation

        // The live dataset-scoped GET operation is retained because the body-based POST operation
        // currently returns HTTP 500; continuation adds only the service-owned cursor value.
        var parameters = new List<string>
        {
            $"q={Uri.EscapeDataString(request.Query)}",
            $"defaultOp={Uri.EscapeDataString(request.DefaultOp)}",
            $"rows={request.Rows}",
            $"fl={Uri.EscapeDataString(string.Join(',', request.Fields))}"
        };

        // Optional arrays are encoded as one comma-separated GET value because the iSearch OpenAPI
        // contract disables explode for these parameters. Encoding the complete value preserves
        // spaces, quotes, range brackets, and other Lucene characters as data.
        if (request.QueryFields is { Count: > 0 })
        {
            parameters.Add($"qf={Uri.EscapeDataString(string.Join(',', request.QueryFields))}");
        }

        if (request.FilterQueries is { Count: > 0 })
        {
            parameters.Add($"fq={Uri.EscapeDataString(string.Join(',', request.FilterQueries))}");
        }

        if (!string.IsNullOrWhiteSpace(request.UpdatedBefore))
        {
            parameters.Add($"updatedBefore={Uri.EscapeDataString(request.UpdatedBefore)}");
        }

        if (!string.IsNullOrWhiteSpace(request.UpdatedAfter))
        {
            parameters.Add($"updatedAfter={Uri.EscapeDataString(request.UpdatedAfter)}");
        }

        var requestUri = $"search/{Uri.EscapeDataString(request.Dataset)}?{string.Join('&', parameters)}";

        return string.IsNullOrWhiteSpace(cursor)
            ? requestUri
            : $"{requestUri}&cursor={Uri.EscapeDataString(cursor)}";

        #endregion
    }

    /**************************************************************/
    /// <summary>Validates an optional iSearch update-date request value.</summary>
    /// <param name="value">The date value supplied by the request, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the value is absent or exactly <c>yyyy-MM-dd</c>.</returns>
    /// <remarks>The interactive builder performs the same validation before confirmation; this second gate protects other callers of the API boundary.</remarks>
    private static bool isValidDate(string? value)
    {
        #region implementation

        return string.IsNullOrWhiteSpace(value)
            || DateOnly.TryParseExact(
                value,
                "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out _);

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

        // Missing credentials disable only this operation. Returning the collected messages avoids
        // constructing a request that could never authenticate and lets the caller display all gaps.
        if (credentialResult.Status == OperationStatus.Failure)
        {
            return OperationResult<TResponse>.Failure(credentialResult.Messages);
        }

        // Reject invalid safety settings locally so a malformed configuration cannot create an unbounded operation.
        // Timeout is checked separately because its diagnostic identifies the setting most directly.
        if (_options.TimeoutSeconds <= 0)
        {
            return failure<TResponse>("isearch.configuration.timeout-invalid", "iSearch timeout must be greater than zero seconds.");
        }

        // The remaining settings share one bounded-policy diagnostic because all of them constrain
        // retry volume, pacing, or the amount of upstream data accepted into memory.
        if (_options.TransientRetryCount < 0
            || _options.MinimumRequestIntervalMilliseconds < 0
            || _options.MaximumResponseCharacters <= 0
            || _options.MaximumFieldsResponseCharacters <= 0)
        {
            return failure<TResponse>("isearch.configuration.invalid", "iSearch retry, pacing, and response-limit settings are invalid.");
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        var operationToken = timeoutSource.Token;

        // Each retry receives a fresh request because HttpRequestMessage and its content are single-use resources.
        for (var attempt = 0; attempt <= _options.TransientRetryCount; attempt++)
        {
            // All expected failures below are classified here so a caller receives a safe operation
            // result, while caller cancellation remains the one intentionally propagated exception.
            try
            {
                await waitForRequestSlotAsync(operationToken).ConfigureAwait(false);
                using var request = createRequest(requestUri, method, contentFactory);
                using var response = await _httpClient
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, operationToken)
                    .ConfigureAwait(false);

                // A redirect could move the authenticated cookie to a host outside the fixed iSearch service.
                // Reject it before success/failure handling so no redirect response can be followed or retried.
                if (isRedirect(response.StatusCode))
                {
                    return failure<TResponse>("isearch.redirect", $"iSearch returned redirect status {(int)response.StatusCode}; redirects are disabled.");
                }

                // Successful responses are parsed immediately; parsing failures are handled by the
                // JSON-specific catch below and are never mistaken for transport failures.
                if (response.IsSuccessStatusCode)
                {
                    return await parseResponseAsync(response, operationToken).ConfigureAwait(false);
                }

                // Retry only statuses that are explicitly safe to repeat without changing operator input.
                // The attempt check leaves the final response available for a useful bounded error message.
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

                // Include diagnostic text only when the server supplied non-whitespace content; this
                // keeps ordinary status failures concise without losing useful service context.
                var bodySuffix = string.IsNullOrWhiteSpace(responseBody)
                    ? string.Empty
                    : $" Response: {responseBody}";
                return failure<TResponse>(
                    "isearch.http",
                    $"iSearch {operation} returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase ?? "Unknown Status"}).{bodySuffix}");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The caller owns this cancellation request, so preserve its standard cancellation
                // semantics instead of converting an intentional stop into an operation failure.
                throw;
            }
            catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
            {
                // This filter identifies cancellation caused by the client-owned timeout rather
                // than by the caller, allowing the CLI to report a useful timeout diagnostic.
                return failure<TResponse>("isearch.timeout", $"The iSearch {operation} operation exceeded its configured timeout.");
            }
            catch (HttpRequestException exception) when (attempt < _options.TransientRetryCount)
            {
                // A transport failure has no HTTP status, but repeating it is still allowed while
                // retry budget remains. The next loop iteration creates a fresh authenticated request.
                _logger.LogWarning(
                    "iSearch {Operation} transport attempt {AttemptNumber} failed and will be retried: {Message}",
                    operation,
                    attempt + 1,
                    exception.Message);
                await Task.Delay(getRetryDelay(null, attempt), operationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException exception)
            {
                // With no retry budget left, return the bounded transport diagnostic to the caller.
                return failure<TResponse>("isearch.transport", $"Could not contact iSearch for {operation}: {exception.Message}");
            }
            catch (JsonException exception)
            {
                // A syntactically or structurally invalid successful payload is not transient; a
                // retry would repeat the same contract problem and obscure its cause.
                return failure<TResponse>("isearch.response.malformed", $"iSearch returned malformed JSON for {operation}: {exception.Message}");
            }
        }

        // Reaching this point means every configured attempt was consumed by a retryable transport
        // path without producing a response that could be returned or parsed.
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

        // Check each credential independently so an operator can correct all missing values in one
        // edit rather than discovering them serially across repeated command runs.
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

        // An empty message list is the only success state; any missing credential keeps the
        // authenticated boundary closed.
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
            // The first request has no prior timestamp and therefore proceeds without an artificial delay.
            if (last != 0 && _options.MinimumRequestIntervalMilliseconds > 0)
            {
                var elapsed = Stopwatch.GetElapsedTime(last);
                var remaining = TimeSpan.FromMilliseconds(_options.MinimumRequestIntervalMilliseconds) - elapsed;

                // If request work already consumed the interval, no delay is needed; otherwise wait
                // only for the unelapsed remainder and keep cancellation responsive.
                if (remaining > TimeSpan.Zero)
                {
                    await Task.Delay(remaining, cancellationToken).ConfigureAwait(false);
                }
            }

            Interlocked.Exchange(ref _lastRequestTimestamp, Stopwatch.GetTimestamp());
        }
        finally
        {
            // Release the gate on success, cancellation, or failure so one interrupted request
            // cannot permanently block every later iSearch operation.
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

        // GET operations omit content; a body is attached only when the caller supplied a factory
        // capable of creating fresh content for the current request attempt.
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

        // Truncate only oversized diagnostics. The response status remains the primary error, while
        // this bound prevents an upstream failure body from flooding the terminal or logs.
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

        // Health is expected to be an object because the interactive flow reads its status and
        // displays the payload. A different JSON shape cannot support that contract safely.
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return failure<SearchHealthResponse>("isearch.response.malformed", "iSearch health returned a non-object JSON payload.");
        }

        // Keep a missing or non-string status as null. The caller then treats it as unavailable
        // instead of assuming that an incomplete health payload means the service is ready.
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

        // Dataset discovery is an array contract; accepting an object or scalar would create
        // choices that do not correspond to the service's advertised dataset list.
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return failure<IReadOnlyList<string>>("isearch.response.malformed", "iSearch datasets returned a non-array JSON payload.");
        }

        var datasets = new List<string>();

        // Validate every element before adding it so the returned list is either wholly usable or
        // rejected as one malformed service response.
        foreach (var element in document.RootElement.EnumerateArray())
        {
            // Empty and non-string entries cannot be selected or safely encoded into later paths.
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
    /// <summary>Parses the field array and requires each item to provide a nonempty name.</summary>
    /// <param name="response">The successful HTTP response.</param>
    /// <param name="cancellationToken">The operation cancellation token.</param>
    /// <returns>The field definitions or a malformed-response failure.</returns>
    private async Task<OperationResult<IReadOnlyList<SearchField>>> parseFieldsAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        #region implementation

        using var document = await readJsonAsync(
            response,
            cancellationToken,
            _options.MaximumFieldsResponseCharacters).ConfigureAwait(false);

        // Fields must be an array because each item becomes one selectable/displayable field
        // definition; the dedicated size limit allows a larger schema without widening other bodies.
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return failure<IReadOnlyList<SearchField>>("isearch.response.malformed", "iSearch fields returned a non-array JSON payload.");
        }

        var fields = new List<SearchField>();

        // Preserve the service's field order while validating each item. One invalid item invalidates
        // the whole schema because a partial schema could produce an incomplete query or display.
        foreach (var element in document.RootElement.EnumerateArray())
        {
            // Only the name is mandatory. Optional metadata is parsed separately and remains null
            // when the service omits it or uses an incompatible JSON type.
            if (element.ValueKind != JsonValueKind.Object
                || !element.TryGetProperty("name", out var nameElement)
                || nameElement.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(nameElement.GetString()))
            {
                return failure<IReadOnlyList<SearchField>>("isearch.response.malformed", "iSearch fields must contain objects with nonempty names.");
            }

            fields.Add(new SearchField
            {
                Name = nameElement.GetString()!,
                DisplayName = getOptionalString(element, "displayName"),
                FieldType = getOptionalString(element, "fieldType"),
                DefaultQueryField = getOptionalBoolean(element, "defaultQueryField"),
                DefaultResultField = getOptionalBoolean(element, "defaultResultField"),
                MultiValued = getOptionalBoolean(element, "multiValued"),
                SearchOnly = getOptionalBoolean(element, "searchOnly")
            });
        }

        return OperationResult<IReadOnlyList<SearchField>>.Success(fields);

        #endregion
    }

    /**************************************************************/
    /// <summary>Reads an optional JSON string without rejecting omitted or null metadata.</summary>
    /// <param name="element">The field object to inspect.</param>
    /// <param name="propertyName">The JSON property name.</param>
    /// <returns>The service string or <see langword="null"/> when it is absent or non-string.</returns>
    private static string? getOptionalString(JsonElement element, string propertyName)
    {
        #region implementation

        // Treat omitted, null, and wrongly typed optional metadata uniformly as unavailable rather
        // than manufacturing a value that the service did not provide.
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

        #endregion
    }

    /**************************************************************/
    /// <summary>Reads an optional JSON Boolean without inventing a value when it is omitted.</summary>
    /// <param name="element">The field object to inspect.</param>
    /// <param name="propertyName">The JSON property name.</param>
    /// <returns>The service Boolean or <see langword="null"/> when it is absent or not Boolean.</returns>
    private static bool? getOptionalBoolean(JsonElement element, string propertyName)
    {
        #region implementation

        // Preserve the same nullable contract for optional Boolean flags; only JSON true/false is
        // accepted as evidence that the service supplied the flag.
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? property.GetBoolean()
            : null;

        #endregion
    }

    /**************************************************************/
    /// <summary>Parses the documented search envelope and derives its service-page cardinality.</summary>
    /// <param name="response">The successful HTTP response.</param>
    /// <param name="rows">The bounded number of records requested for each service page.</param>
    /// <param name="pageNumber">The one-based service page represented by this response.</param>
    /// <param name="cancellationToken">The operation cancellation token.</param>
    /// <returns>The search envelope with typed cardinality or a malformed-response failure.</returns>
    private static async Task<OperationResult<SearchResponse>> parseSearchAsync(
        HttpResponseMessage response,
        int rows,
        int pageNumber,
        CancellationToken cancellationToken)
    {
        #region implementation

        using var document = await readJsonAsync(response, cancellationToken).ConfigureAwait(false);

        // Validate the envelope as one unit before reading any values. This prevents a partially
        // populated response from being mistaken for a walkable page.
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("returnedCount", out var returnedCount)
            || !document.RootElement.TryGetProperty("totalCount", out var totalCount)
            || !document.RootElement.TryGetProperty("results", out var results)
            || !returnedCount.TryGetInt32(out var returned)
            || !totalCount.TryGetInt32(out var total)
            || rows is < 1 or > MaximumRows
            || pageNumber < 1
            || results.ValueKind != JsonValueKind.Array)
        {
            return failure<SearchResponse>("isearch.response.malformed", "iSearch search returned an incomplete response envelope.");
        }

        // Clone elements before disposing the document so generic records remain valid after parsing returns.
        var responseRecords = results.EnumerateArray().Select(item => item.Clone()).ToArray();

        // Counts must be nonnegative and the current page cannot contain more records than the
        // reported total; rejecting contradictions protects page walkers from bad stop conditions.
        if (returned < 0 || total < 0 || returned > total)
        {
            return failure<SearchResponse>("isearch.response.malformed", "iSearch search returned invalid result counts.");
        }

        // A service page cannot exceed the requested row bound or the derived result-page range;
        // rejecting either contradiction prevents a crawler from skipping or repeating a page.
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling((double)total / rows);
        if (returned > rows || (total > 0 && pageNumber > totalPages))
        {
            return failure<SearchResponse>("isearch.response.malformed", "iSearch search returned invalid result-page metadata.");
        }

        // A present cursor must be a string or explicit null. Treating an object or number as an
        // absent cursor would hide a malformed continuation contract from future page walkers.
        if (document.RootElement.TryGetProperty("cursor", out var cursorValue)
            && cursorValue.ValueKind is not JsonValueKind.String and not JsonValueKind.Null)
        {
            return failure<SearchResponse>("isearch.response.malformed", "iSearch search returned an invalid continuation cursor.");
        }

        // A zero-result initial response has no service page; this gives future walkers an
        // unambiguous stop condition. Nonempty responses retain the caller-supplied service page.
        var responseValue = new SearchResponse
        {
            Cardinality = new SearchCardinality
            {
                TotalResults = total,
                CurrentResults = returned,
                PageNumber = total == 0 ? 0 : pageNumber,
                TotalPages = totalPages
            },
            Results = responseRecords,
            // Cursor is optional at the end of a walk. Preserve it only when the service gives a
            // string; a different type is treated as absent rather than becoming a usable token.
            Cursor = document.RootElement.TryGetProperty("cursor", out var cursor)
                && cursor.ValueKind == JsonValueKind.String
                ? cursor.GetString()
                : null
        };

        // The count-to-record invariant is required for reliable walking: if it fails, callers
        // cannot know whether the page is complete or whether records were silently dropped.
        return responseValue.Cardinality.CurrentResults == responseValue.Results.Count
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

        // A positive limit is opt-in. Zero means the general parser has no extra bound, while a
        // configured limit rejects the body before JsonDocument allocates an oversized tree.
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
            // Prefer a valid duration supplied by the service, but cap it so one response cannot
            // make an interactive command appear hung indefinitely.
            return TimeSpan.FromSeconds(Math.Min(delta.TotalSeconds, 30));
        }

        if (response?.Headers.RetryAfter?.Date is { } date)
        {
            var delay = date - DateTimeOffset.UtcNow;

            // A past date is ignored because it cannot represent a useful wait interval.
            if (delay >= TimeSpan.Zero)
            {
                return TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds, 30));
            }
        }

        // When the server gives no usable guidance, exponential backoff spaces repeated attempts
        // while the thirty-second cap keeps the CLI responsive.
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

        // Retry only request timeout, rate-limit, and server-side failures. Other statuses usually
        // describe caller input or authorization and repeating them cannot change the outcome.
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

        // Redirects are a separate policy decision from transient failures because following one
        // could transfer the authentication cookie to an unintended host.
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
