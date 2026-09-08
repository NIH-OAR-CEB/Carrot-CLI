using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
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
            DefaultOp = "AND",
            Rows = 100
        }, CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(
            "https://isearch.test/api/search/live-dataset?q=vaccine%20%22phase%201%22&defaultOp=AND&rows=100",
            request.RequestUri!.AbsoluteUri);
        Assert.Empty(handler.RequestBodies);
        Assert.Equal(1, result.Value!.ReturnedCount);
        Assert.Equal(7, result.Value.Results[0].GetProperty("id").GetInt32());

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
