using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Carrot.Cli.ISearch;
using Carrot.Cli.ISearch.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Carrot.Cli.Tests.ISearch;

/**************************************************************/
/// <summary>Verifies iSearch request safety, serialization, contract validation, and retry behavior.</summary>
public sealed class ISearchApiClientTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Ensures missing credentials stop the operation before the handler sees a request.</summary>
    [Fact]
    public async Task GetHealthAsync_MissingApiKey_DoesNotSendRequest()
    {
        #region implementation

        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = createClient(handler, new ISearchOptions { ContactEmail = "operator@example.org" });

        var result = await client.GetHealthAsync(CancellationToken.None);

        Assert.Empty(handler.Requests);
        Assert.Equal("isearch.configuration.api-key-missing", result.Messages[0].Code);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures health uses the fixed endpoint and sends credentials only in headers.</summary>
    [Fact]
    public async Task GetHealthAsync_ValidConfiguration_SendsSafeRequest()
    {
        #region implementation

        var handler = new RecordingHandler(_ => json(HttpStatusCode.OK, "{\"status\":\"UP\",\"state\":\"ready\"}"));
        var client = createClient(handler, validOptions());

        var result = await client.GetHealthAsync(CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://isearch.test/api/health", request.RequestUri!.AbsoluteUri);
        Assert.Equal("apiKey=synthetic-test-key", request.Headers.GetValues("Cookie").Single());
        Assert.Equal("operator@example.org", request.Headers.GetValues("From").Single());
        Assert.Equal("UP", result.Value!.Status);
        Assert.Contains("ready", result.Value.Payload.GetRawText(), StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures the dataset-scoped GET search encodes the dataset, query, and bounded controls.</summary>
    [Fact]
    public async Task SearchAsync_UsesEncodedDatasetScopedGetAndParsesRecords()
    {
        #region implementation

        var handler = new RecordingHandler(_ => json(
            HttpStatusCode.OK,
            "{\"cursor\":\"next\",\"returnedCount\":1,\"totalCount\":4,\"results\":[{\"id\":7,\"label\":\"example\"}]}"));
        var client = createClient(handler, validOptions());

        var result = await client.SearchAsync(new SearchRequest
        {
            Dataset = "live-dataset",
            Query = "vaccine \"phase 1\"",
            Fields = ["title", "abstract"],
            DefaultOp = "AND",
            Rows = 100
        }, CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(
            "https://isearch.test/api/search/live-dataset?q=vaccine%20%22phase%201%22&defaultOp=AND&rows=100&fl=title%2Cabstract",
            request.RequestUri!.AbsoluteUri);
        Assert.Empty(handler.RequestBodies);
        Assert.Equal(1, result.Value!.Cardinality.CurrentResults);
        Assert.Equal(4, result.Value.Cardinality.TotalResults);
        Assert.Equal(1, result.Value.Cardinality.PageNumber);
        Assert.Equal(1, result.Value.Cardinality.TotalPages);
        Assert.Equal("next", result.Value.Cursor);
        Assert.Equal(7, result.Value.Results[0].GetProperty("id").GetInt32());

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures advanced query controls are encoded as optional dataset-scoped GET values.</summary>
    [Fact]
    public async Task SearchAsync_EncodesAdvancedQueryControlsWithoutChangingTransport()
    {
        #region implementation

        var handler = new RecordingHandler(_ => json(
            HttpStatusCode.OK,
            "{\"returnedCount\":0,\"totalCount\":0,\"results\":[]}"));
        var client = createClient(handler, validOptions());

        await client.SearchAsync(new SearchRequest
        {
            Dataset = "live grants",
            Query = "pain study",
            QueryFields = ["title", "abstract"],
            FilterQueries = ["fy:2024", "fundingCategory:\"Research Project Grants\""],
            Fields = ["id", "title"],
            DefaultOp = "OR",
            Rows = 25,
            UpdatedAfter = "2024-10-01",
            UpdatedBefore = "2025-09-30"
        }, CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(
            "https://isearch.test/api/search/live%20grants?q=pain%20study&defaultOp=OR&rows=25&fl=id%2Ctitle&qf=title%2Cabstract&fq=fy%3A2024%2CfundingCategory%3A%22Research%20Project%20Grants%22&updatedBefore=2025-09-30&updatedAfter=2024-10-01",
            request.RequestUri!.AbsoluteUri);
        Assert.Empty(handler.RequestBodies);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures invalid advanced controls fail before an HTTP request is created.</summary>
    [Theory]
    [InlineData("OR", "bad-date", null, 25)]
    [InlineData("XOR", null, null, 25)]
    [InlineData("AND", null, "bad-date", 25)]
    [InlineData("AND", null, null, 101)]
    public async Task SearchAsync_InvalidAdvancedControls_DoesNotSendRequest(
        string defaultOp,
        string? updatedAfter,
        string? updatedBefore,
        int rows)
    {
        #region implementation

        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = createClient(handler, validOptions());

        var result = await client.SearchAsync(new SearchRequest
        {
            Dataset = "live-dataset",
            Query = "vaccine",
            Fields = ["title"],
            DefaultOp = defaultOp,
            Rows = rows,
            UpdatedAfter = updatedAfter,
            UpdatedBefore = updatedBefore
        }, CancellationToken.None);

        Assert.Empty(handler.Requests);
        Assert.Equal(OperationStatus.Failure, result.Status);
        Assert.Equal("isearch.request.invalid", result.Messages[0].Code);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures result-page cardinality uses the request row limit and terminates cleanly for empty data.</summary>
    [Theory]
    [InlineData(2, 5, 2, 3)]
    [InlineData(1, 1, 100, 1)]
    [InlineData(0, 0, 100, 0)]
    public async Task SearchAsync_DerivesResultPageCardinality(
        int returnedCount,
        int totalCount,
        int rows,
        int expectedTotalPages)
    {
        #region implementation

        // Empty test responses intentionally omit record objects; nonempty responses synthesize
        // exactly the declared count so the parser's count-to-record invariant is exercised.
        var records = returnedCount == 0
            ? string.Empty
            : string.Join(',', Enumerable.Range(0, returnedCount).Select(index => $"{{\"id\":{index}}}"));
        var handler = new RecordingHandler(_ => json(
            HttpStatusCode.OK,
            $"{{\"returnedCount\":{returnedCount},\"totalCount\":{totalCount},\"results\":[{records}]}}"));
        var client = createClient(handler, validOptions());

        var result = await client.SearchAsync(new SearchRequest
        {
            Dataset = "live-dataset",
            Query = "vaccine",
            Fields = ["title"],
            DefaultOp = "AND",
            Rows = rows
        }, CancellationToken.None);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(totalCount, result.Value!.Cardinality.TotalResults);
        Assert.Equal(returnedCount, result.Value.Cardinality.CurrentResults);
        // Zero results use page zero; every nonempty response represents the first service page.
        Assert.Equal(totalCount == 0 ? 0 : 1, result.Value.Cardinality.PageNumber);
        Assert.Equal(expectedTotalPages, result.Value.Cardinality.TotalPages);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures continuation preserves the query context and advances service cardinality.</summary>
    [Fact]
    public async Task SearchNextPageAsync_UsesCursorAndPreservesQueryContext()
    {
        #region implementation

        var callNumber = 0;
        var handler = new RecordingHandler(_ =>
        {
            callNumber++;
            return json(
                HttpStatusCode.OK,
                "{\"cursor\":\"final token\",\"returnedCount\":1,\"totalCount\":11,\"results\":[{\"id\":11}]}");
        });
        var client = createClient(handler, validOptions());
        var request = new SearchRequest
        {
            Dataset = "live/dataset",
            Query = "vaccine \"phase 1\"",
            Fields = ["title", "abstract"],
            DefaultOp = "AND",
            Rows = 10
        };

        var result = await client.SearchNextPageAsync(request, "next cursor/with spaces", 2, CancellationToken.None);

        var outgoingRequest = Assert.Single(handler.Requests);
        Assert.Equal(1, callNumber);
        Assert.Equal(
            "https://isearch.test/api/search/live%2Fdataset?q=vaccine%20%22phase%201%22&defaultOp=AND&rows=10&fl=title%2Cabstract&cursor=next%20cursor%2Fwith%20spaces",
            outgoingRequest.RequestUri!.AbsoluteUri);
        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(11, result.Value!.Cardinality.TotalResults);
        Assert.Equal(2, result.Value.Cardinality.PageNumber);
        Assert.Equal(2, result.Value.Cardinality.TotalPages);
        Assert.Equal("final token", result.Value.Cursor);
        Assert.Equal(11, result.Value.Results[0].GetProperty("id").GetInt32());

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures advanced controls remain unchanged when a cursor page is requested.</summary>
    [Fact]
    public async Task SearchNextPageAsync_PreservesAdvancedQueryControls()
    {
        #region implementation

        var handler = new RecordingHandler(_ => json(
            HttpStatusCode.OK,
            "{\"returnedCount\":1,\"totalCount\":2,\"results\":[{\"id\":2}]}"));
        var client = createClient(handler, validOptions());

        await client.SearchNextPageAsync(new SearchRequest
        {
            Dataset = "live-dataset",
            Query = "*:*",
            QueryFields = ["title"],
            FilterQueries = ["fy:2024"],
            Fields = ["id"],
            DefaultOp = "AND",
            Rows = 1,
            UpdatedAfter = "2024-10-01",
            UpdatedBefore = "2025-09-30"
        }, "next cursor", 2, CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(
            "https://isearch.test/api/search/live-dataset?q=%2A%3A%2A&defaultOp=AND&rows=1&fl=id&qf=title&fq=fy%3A2024&updatedBefore=2025-09-30&updatedAfter=2024-10-01&cursor=next%20cursor",
            request.RequestUri!.AbsoluteUri);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures unusable continuation state fails before credentials or HTTP are used.</summary>
    [Theory]
    [InlineData(" ", 2)]
    [InlineData("cursor", 0)]
    public async Task SearchNextPageAsync_InvalidContinuation_DoesNotSendRequest(string cursor, int pageNumber)
    {
        #region implementation

        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = createClient(handler, validOptions());

        var result = await client.SearchNextPageAsync(new SearchRequest
        {
            Dataset = "live-dataset",
            Query = "vaccine",
            Fields = ["title"],
            Rows = 100
        }, cursor, pageNumber, CancellationToken.None);

        Assert.Empty(handler.Requests);
        Assert.Equal(OperationStatus.Failure, result.Status);
        Assert.Equal("isearch.paging.invalid", result.Messages[0].Code);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures invalid result counts are rejected before a response can be consumed.</summary>
    [Theory]
    [InlineData("{\"returnedCount\":-1,\"totalCount\":0,\"results\":[]}")]
    [InlineData("{\"returnedCount\":2,\"totalCount\":1,\"results\":[{\"id\":1},{\"id\":2}]}")]
    [InlineData("{\"cursor\":{},\"returnedCount\":0,\"totalCount\":0,\"results\":[]}")]
    public async Task SearchAsync_InvalidResultCounts_ReturnsMalformedFailure(string payload)
    {
        #region implementation

        var handler = new RecordingHandler(_ => json(HttpStatusCode.OK, payload));
        var client = createClient(handler, validOptions());

        var result = await client.SearchAsync(new SearchRequest
        {
            Dataset = "live-dataset",
            Query = "vaccine",
            Fields = ["title"],
            DefaultOp = "AND",
            Rows = 100
        }, CancellationToken.None);

        Assert.Equal(OperationStatus.Failure, result.Status);
        Assert.Equal("isearch.response.malformed", result.Messages[0].Code);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures a search with no configured result fields is rejected before HTTP.</summary>
    [Fact]
    public async Task SearchAsync_EmptyFields_DoesNotSendRequest()
    {
        #region implementation

        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = createClient(handler, validOptions());

        var result = await client.SearchAsync(new SearchRequest
        {
            Dataset = "live-dataset",
            Query = "vaccine",
            DefaultOp = "AND",
            Rows = 100
        }, CancellationToken.None);

        Assert.Empty(handler.Requests);
        Assert.Equal("isearch.request.invalid", result.Messages[0].Code);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures field discovery uses an encoded authenticated GET and preserves metadata.</summary>
    [Fact]
    public async Task GetFieldsAsync_UsesEncodedDatasetPathAndParsesMetadata()
    {
        #region implementation

        var handler = new RecordingHandler(_ => json(
            HttpStatusCode.OK,
            "[{\"name\":\"grantNumber\",\"displayName\":\"Grant [number]\",\"fieldType\":\"string\",\"defaultQueryField\":true,\"defaultResultField\":false,\"multiValued\":false,\"searchOnly\":true},{\"name\":\"optional\"}]"));
        var client = createClient(handler, validOptions());

        var result = await client.GetFieldsAsync("grants/live", CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://isearch.test/api/fields/grants%2Flive", request.RequestUri!.AbsoluteUri);
        Assert.Equal("apiKey=synthetic-test-key", request.Headers.GetValues("Cookie").Single());
        Assert.Equal("operator@example.org", request.Headers.GetValues("From").Single());
        Assert.Equal("grantNumber", result.Value![0].Name);
        Assert.Equal("Grant [number]", result.Value[0].DisplayName);
        Assert.Equal("string", result.Value[0].FieldType);
        Assert.True(result.Value[0].DefaultQueryField);
        Assert.False(result.Value[0].DefaultResultField);
        Assert.False(result.Value[0].MultiValued);
        Assert.True(result.Value[0].SearchOnly);
        Assert.Null(result.Value[1].DisplayName);
        Assert.Null(result.Value[1].DefaultQueryField);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures an empty dataset name fails before an authenticated request is constructed.</summary>
    [Fact]
    public async Task GetFieldsAsync_EmptyDataset_DoesNotSendRequest()
    {
        #region implementation

        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = createClient(handler, validOptions());

        var result = await client.GetFieldsAsync(" ", CancellationToken.None);

        Assert.Empty(handler.Requests);
        Assert.Equal("isearch.request.invalid", result.Messages[0].Code);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures non-array and unnamed field responses become safe malformed failures.</summary>
    [Theory]
    [InlineData("{}")]
    [InlineData("[{\"displayName\":\"Unnamed\"}]")]
    [InlineData("[{\"name\":\" \"}]")]
    public async Task GetFieldsAsync_InvalidPayload_ReturnsMalformedFailure(string payload)
    {
        #region implementation

        var handler = new RecordingHandler(_ => json(HttpStatusCode.OK, payload));
        var client = createClient(handler, validOptions());

        var result = await client.GetFieldsAsync("grants", CancellationToken.None);

        Assert.Equal("isearch.response.malformed", result.Messages[0].Code);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures field discovery accepts a schema larger than the general response bound.</summary>
    [Fact]
    public async Task GetFieldsAsync_LargeFieldPayload_UsesDedicatedFieldLimit()
    {
        #region implementation

        var payload = $"[{{\"name\":\"large-field\",\"displayName\":\"{new string('x', 9_000)}\"}}]";
        var handler = new RecordingHandler(_ => json(HttpStatusCode.OK, payload));
        var client = createClient(handler, validOptions());

        var result = await client.GetFieldsAsync("grants", CancellationToken.None);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal("large-field", result.Value![0].Name);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures the dedicated field bound still rejects an unbounded successful payload.</summary>
    [Fact]
    public async Task GetFieldsAsync_ExceedsDedicatedFieldLimit_ReturnsMalformedFailure()
    {
        #region implementation

        var options = validOptions();
        options.MaximumFieldsResponseCharacters = 100;
        var handler = new RecordingHandler(_ => json(
            HttpStatusCode.OK,
            $"[{{\"name\":\"large-field\",\"displayName\":\"{new string('x', 200)}\"}}]"));
        var client = createClient(handler, options);

        var result = await client.GetFieldsAsync("grants", CancellationToken.None);

        Assert.Equal("isearch.response.malformed", result.Messages[0].Code);

        #endregion
    }

    /**************************************************************/
    /// <summary>Includes a bounded server diagnostic when a non-success response is returned.</summary>
    [Fact]
    public async Task SearchAsync_ServerFailure_IncludesResponseBodyWithoutCredential()
    {
        #region implementation

        var handler = new RecordingHandler(_ => json(
            HttpStatusCode.InternalServerError,
            "{\"message\":\"support code: synthetic-error\"}"));
        var options = validOptions();
        options.TransientRetryCount = 0;
        var client = createClient(handler, options);

        var result = await client.SearchAsync(new SearchRequest
        {
            Dataset = "live-dataset",
            Query = "vaccine",
            Fields = ["title"],
            DefaultOp = "AND",
            Rows = 1
        }, CancellationToken.None);

        Assert.Contains("synthetic-error", result.Messages[0].Message, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-test-key", result.Messages[0].Message, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures redirects are rejected and never trigger an unchanged retry.</summary>
    [Fact]
    public async Task GetDatasetsAsync_Redirect_ReturnsFailureWithoutRetry()
    {
        #region implementation

        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.TemporaryRedirect));
        var client = createClient(handler, validOptions());

        var result = await client.GetDatasetsAsync(CancellationToken.None);

        Assert.Single(handler.Requests);
        Assert.Equal("isearch.redirect", result.Messages[0].Code);
        Assert.DoesNotContain("synthetic-test-key", result.Messages[0].Message, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures a transient response honors retry policy without leaking response content.</summary>
    [Fact]
    public async Task GetDatasetsAsync_TooManyRequests_RetriesAndThenSucceeds()
    {
        #region implementation

        var callCount = 0;
        // Return a transient first response to exercise retry selection, then a successful dataset
        // response so the test proves the second attempt is actually reached.
        var handler = new RecordingHandler(_ => ++callCount == 1
            ? new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Headers = { RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero) }
            }
            : json(HttpStatusCode.OK, "[\"grants\"]"));
        var options = validOptions();
        options.TransientRetryCount = 1;
        var client = createClient(handler, options);

        var result = await client.GetDatasetsAsync(CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(["grants"], result.Value);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates an API client over a deterministic test handler.</summary>
    /// <param name="handler">The handler recording outgoing requests.</param>
    /// <param name="options">The iSearch options used by the client.</param>
    /// <returns>The client under test.</returns>
    private static IISearchApiClient createClient(RecordingHandler handler, ISearchOptions options)
    {
        #region implementation

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://isearch.test/api/") };
        return new ISearchApiClient(httpClient, Options.Create(options), NullLogger<ISearchApiClient>.Instance);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates safe synthetic credentials and test-friendly timing values.</summary>
    /// <returns>Options with no real credential.</returns>
    private static ISearchOptions validOptions() => new()
    {
        ApiKey = "synthetic-test-key",
        ContactEmail = "operator@example.org",
        TimeoutSeconds = 10,
        TransientRetryCount = 2,
        MinimumRequestIntervalMilliseconds = 0
    };

    /**************************************************************/
    /// <summary>Creates an HTTP JSON response for the fake service.</summary>
    /// <param name="statusCode">The response status.</param>
    /// <param name="body">The JSON response body.</param>
    /// <returns>The disposable response.</returns>
    private static HttpResponseMessage json(HttpStatusCode statusCode, string body)
    {
        #region implementation

        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>Records cloned request metadata and delegates responses to a supplied factory.</summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        #region implementation

        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        /**************************************************************/
        /// <summary>Initializes the handler with its deterministic response factory.</summary>
        /// <param name="responseFactory">The response factory.</param>
        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        /**************************************************************/
        /// <summary>Gets the requests observed by the handler.</summary>
        public List<HttpRequestMessage> Requests { get; } = [];

        /**************************************************************/
        /// <summary>Gets request bodies captured before the client disposes each request.</summary>
        public List<string> RequestBodies { get; } = [];

        /**************************************************************/
        /// <summary>Records one request and returns the configured response.</summary>
        /// <param name="request">The request sent by the client.</param>
        /// <param name="cancellationToken">The ignored test cancellation token.</param>
        /// <returns>The fake response.</returns>
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            #region implementation

            Requests.Add(request);
            // GET requests have no body in this client, but retain this branch so the test handler
            // remains correct if a body-based operation is added later.
            if (request.Content is not null)
            {
                RequestBodies.Add(request.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            }
            return Task.FromResult(_responseFactory(request));

            #endregion
        }

        #endregion
    }

    #endregion
}
