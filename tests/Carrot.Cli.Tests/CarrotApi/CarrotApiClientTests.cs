using System.Net;
using System.Text;
using System.Text.Json;
using Carrot.Cli.CarrotApi;
using Carrot.Cli.CarrotApi.Contracts;
using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Carrot.Cli.Tests.CarrotApi;

/**************************************************************/
/// <summary>Verifies URL, JSON, headers, errors, redirect safety, retry, timeout, and cancellation behavior.</summary>
public sealed class CarrotApiClientTests
{
    #region implementation

    private static readonly Uri ServiceEndpoint = new("https://carrot.example/service");

    /**************************************************************/
    /// <summary>Verifies both public API methods construct exact URLs, headers, and cluster JSON.</summary>
    [Fact]
    public async Task PublicApiMethods_ValidRequests_SendExactJsonAndHeaders()
    {
        #region implementation

        // Arrange
        var handler = new RecordingHandler((_, _) => Task.FromResult(
            handlerCallResponse(handlerCall: 0)));
        handler.ResponseFactory = (_, _) => Task.FromResult(handlerCallResponse(handler.CallCount));
        var client = createClient(handler);
        var request = new ClusterRequest
        {
            Algorithm = "Lingo",
            Language = "English",
            Documents =
            [
                new ClusterDocument { Title = "First", Content = "Complete first text" },
                new ClusterDocument { Title = "Second", Content = "Complete second text" }
            ]
        };

        // Act
        var listResult = await client.GetConfigurationAsync(
            ServiceEndpoint,
            TimeSpan.FromSeconds(2),
            indent: true,
            TestContext.Current.CancellationToken);
        var clusterResult = await client.ClusterAsync(
            ServiceEndpoint,
            request,
            template: "front end",
            TimeSpan.FromSeconds(2),
            indent: false,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OperationStatus.Success, listResult.Status);
        Assert.Equal(OperationStatus.Success, clusterResult.Status);
        Assert.Collection(
            handler.Requests,
            listRequest =>
            {
                Assert.Equal(HttpMethod.Get, listRequest.Method);
                Assert.Equal("https://carrot.example/service/list?indent=true", listRequest.Uri.AbsoluteUri);
                Assert.Equal(["application/json"], listRequest.AcceptMediaTypes);
                Assert.Null(listRequest.ContentMediaType);
            },
            clusterRequest =>
            {
                Assert.Equal(HttpMethod.Post, clusterRequest.Method);
                Assert.Equal(
                    "https://carrot.example/service/cluster?template=front%20end&indent=false",
                    clusterRequest.Uri.AbsoluteUri);
                Assert.Equal(["application/json"], clusterRequest.AcceptMediaTypes);
                Assert.Equal("application/json", clusterRequest.ContentMediaType);
                Assert.True(JsonElement.DeepEquals(
                    JsonSerializer.SerializeToElement(request),
                    JsonDocument.Parse(clusterRequest.Content!).RootElement));
            });

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies two configured retries allow a third attempt for stateless transient statuses.</summary>
    [Fact]
    public async Task GetConfigurationAsync_TransientStatuses_RetriesAtMostConfiguredCount()
    {
        #region implementation

        // Arrange
        var handler = new RecordingHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("Unavailable", Encoding.UTF8, "text/plain")
            }));
        var client = createClient(handler, retryCount: 2);

        // Act
        var result = await client.GetConfigurationAsync(
            ServiceEndpoint,
            TimeSpan.FromSeconds(2),
            indent: null,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OperationStatus.Failure, result.Status);
        Assert.Equal(3, handler.CallCount);
        Assert.Equal("carrot.http", Assert.Single(result.Messages).Code);

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies transport exceptions are retried and can recover within the configured limit.</summary>
    [Fact]
    public async Task GetConfigurationAsync_TransportFailureThenSuccess_ReturnsConfiguration()
    {
        #region implementation

        // Arrange
        var handler = new RecordingHandler((_, _) => Task.FromResult(jsonResponse(HttpStatusCode.OK, listJson())));
        handler.ResponseFactory = (_, _) =>
        {
            if (handler.CallCount == 1)
            {
                throw new HttpRequestException("Temporary connection failure.");
            }

            return Task.FromResult(jsonResponse(HttpStatusCode.OK, listJson()));
        };
        var client = createClient(handler, retryCount: 2);

        // Act
        var result = await client.GetConfigurationAsync(
            ServiceEndpoint,
            TimeSpan.FromSeconds(2),
            indent: null,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(2, handler.CallCount);
        Assert.True(result.Value!.Algorithms.ContainsKey("Lingo"));

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies documented bad-request bodies expose only type/message and are never retried.</summary>
    [Fact]
    public async Task ClusterAsync_BadRequest_TranslatesSafeErrorWithoutRetryOrStackTrace()
    {
        #region implementation

        // Arrange
        const string errorJson = """
            {
              "type": "BAD_REQUEST",
              "message": "The request is invalid.",
              "exception": "SecretServerException",
              "stacktrace": "sensitive stack trace"
            }
            """;
        var handler = new RecordingHandler((_, _) => Task.FromResult(
            jsonResponse(HttpStatusCode.BadRequest, errorJson)));
        var client = createClient(handler);

        // Act
        var result = await client.ClusterAsync(
            ServiceEndpoint,
            new ClusterRequest(),
            template: null,
            TimeSpan.FromSeconds(2),
            indent: null,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OperationStatus.Failure, result.Status);
        Assert.Equal(1, handler.CallCount);
        var message = Assert.Single(result.Messages).Message;
        Assert.Contains("BAD_REQUEST", message, StringComparison.Ordinal);
        Assert.Contains("The request is invalid.", message, StringComparison.Ordinal);
        Assert.DoesNotContain("SecretServerException", message, StringComparison.Ordinal);
        Assert.DoesNotContain("sensitive stack trace", message, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies documented HTTP 500 bodies are retried, then safely translated after exhaustion.</summary>
    [Fact]
    public async Task ClusterAsync_DocumentedServerError_RetriesThenTranslatesTypeAndMessage()
    {
        #region implementation

        // Arrange
        const string errorJson = """
            {
              "type": "UNHANDLED_ERROR",
              "message": "The clustering service failed.",
              "stacktrace": "sensitive server frames"
            }
            """;
        var handler = new RecordingHandler((_, _) => Task.FromResult(
            jsonResponse(HttpStatusCode.InternalServerError, errorJson)));
        var client = createClient(handler);

        // Act
        var result = await client.ClusterAsync(
            ServiceEndpoint,
            new ClusterRequest(),
            template: null,
            TimeSpan.FromSeconds(2),
            indent: null,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OperationStatus.Failure, result.Status);
        Assert.Equal(3, handler.CallCount);
        var message = Assert.Single(result.Messages).Message;
        Assert.Contains("UNHANDLED_ERROR", message, StringComparison.Ordinal);
        Assert.Contains("The clustering service failed.", message, StringComparison.Ordinal);
        Assert.DoesNotContain("sensitive server frames", message, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies redirects are rejected immediately rather than followed or retried.</summary>
    [Fact]
    public async Task ClusterAsync_Redirect_ReturnsSafetyFailureWithoutRetry()
    {
        #region implementation

        // Arrange
        var handler = new RecordingHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.TemporaryRedirect)
            {
                Headers = { Location = new Uri("https://unexpected.example/collect") }
            }));
        var client = createClient(handler);

        // Act
        var result = await client.ClusterAsync(
            ServiceEndpoint,
            new ClusterRequest(),
            template: null,
            TimeSpan.FromSeconds(2),
            indent: null,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OperationStatus.Failure, result.Status);
        Assert.Equal(1, handler.CallCount);
        Assert.Equal("carrot.redirect", Assert.Single(result.Messages).Code);

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies malformed successful and documented error JSON never trigger a retry.</summary>
    /// <param name="statusCode">The status returning malformed JSON.</param>
    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GetConfigurationAsync_MalformedJson_ReturnsContractFailureWithoutRetry(
        HttpStatusCode statusCode)
    {
        #region implementation

        // Arrange
        var handler = new RecordingHandler((_, _) => Task.FromResult(jsonResponse(statusCode, "{")));
        var client = createClient(handler);

        // Act
        var result = await client.GetConfigurationAsync(
            ServiceEndpoint,
            TimeSpan.FromSeconds(2),
            indent: null,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OperationStatus.Failure, result.Status);
        Assert.Equal(1, handler.CallCount);
        Assert.Equal("carrot.response.malformed", Assert.Single(result.Messages).Code);

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies syntactically valid JSON with explicit null contract members is rejected without retry.</summary>
    [Fact]
    public async Task ClusterAsync_NullRequiredCollection_ReturnsContractFailureWithoutRetry()
    {
        #region implementation

        // Arrange
        var handler = new RecordingHandler((_, _) => Task.FromResult(
            jsonResponse(HttpStatusCode.OK, """{ "clusters": null }""")));
        var client = createClient(handler);

        // Act
        var result = await client.ClusterAsync(
            ServiceEndpoint,
            new ClusterRequest(),
            template: null,
            TimeSpan.FromSeconds(2),
            indent: null,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OperationStatus.Failure, result.Status);
        Assert.Equal(1, handler.CallCount);
        Assert.Equal("carrot.response.malformed", Assert.Single(result.Messages).Code);

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies one timeout budget spans all attempts instead of restarting for each retry.</summary>
    [Fact]
    public async Task GetConfigurationAsync_RetryConsumesOverallTimeoutBudget()
    {
        #region implementation

        // Arrange
        var handler = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        handler.ResponseFactory = async (_, cancellationToken) =>
        {
            if (handler.CallCount == 1)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(35), cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The cancellation-aware delay should not complete.");
        };
        var client = createClient(handler);

        // Act
        var result = await client.GetConfigurationAsync(
            ServiceEndpoint,
            TimeSpan.FromMilliseconds(100),
            indent: null,
            CancellationToken.None);

        // Assert
        Assert.Equal(OperationStatus.Failure, result.Status);
        Assert.Equal("carrot.timeout", Assert.Single(result.Messages).Code);
        Assert.Equal(2, handler.CallCount);

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies caller cancellation propagates immediately and is never retried.</summary>
    [Fact]
    public async Task GetConfigurationAsync_CallerCancellation_PropagatesWithoutRetry()
    {
        #region implementation

        // Arrange
        var enteredHandler = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new RecordingHandler(async (_, cancellationToken) =>
        {
            enteredHandler.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The cancellation-aware delay should not complete.");
        });
        var client = createClient(handler);
        using var cancellationSource = new CancellationTokenSource();

        // Act
        var operation = client.GetConfigurationAsync(
            ServiceEndpoint,
            TimeSpan.FromSeconds(2),
            indent: null,
            cancellationSource.Token);
        await enteredHandler.Task;
        await cancellationSource.CancelAsync();

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        Assert.Equal(1, handler.CallCount);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a client with a fake primary handler and deterministic retry configuration.</summary>
    /// <param name="handler">The fake HTTP handler.</param>
    /// <param name="retryCount">The number of retries after the first attempt.</param>
    /// <returns>The API client under test.</returns>
    private static CarrotApiClient createClient(HttpMessageHandler handler, int retryCount = 2)
    {
        #region implementation

        var httpClient = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        return new CarrotApiClient(
            httpClient,
            NullLogger<CarrotApiClient>.Instance,
            Options.Create(new CarrotCliOptions { TransientRetryCount = retryCount }));

        #endregion
    }

    /**************************************************************/
    /// <summary>Returns one response appropriate for the one-based handler invocation.</summary>
    /// <param name="handlerCall">The one-based handler invocation.</param>
    /// <returns>A list response for call one and a cluster response thereafter.</returns>
    private static HttpResponseMessage handlerCallResponse(int handlerCall)
    {
        #region implementation

        return handlerCall <= 1
            ? jsonResponse(HttpStatusCode.OK, listJson())
            : jsonResponse(HttpStatusCode.OK, """{ "clusters": [] }""");

        #endregion
    }

    /**************************************************************/
    /// <summary>Returns the valid configuration JSON shared by client tests.</summary>
    /// <returns>A documented list response.</returns>
    private static string listJson()
    {
        #region implementation

        return """{ "algorithms": { "Lingo": [ "English" ] }, "templates": {} }""";

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one JSON HTTP response.</summary>
    /// <param name="statusCode">The response status.</param>
    /// <param name="json">The response body.</param>
    /// <returns>The response message.</returns>
    private static HttpResponseMessage jsonResponse(HttpStatusCode statusCode, string json)
    {
        #region implementation

        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>Captures requests before returning delegate-controlled responses.</summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        #region implementation

        /**************************************************************/
        /// <summary>Initializes the handler with its response factory.</summary>
        /// <param name="responseFactory">The per-attempt response factory.</param>
        internal RecordingHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        {
            #region implementation

            ResponseFactory = responseFactory;

            #endregion
        }

        /**************************************************************/
        /// <summary>Gets or sets the response factory invoked after request capture.</summary>
        internal Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> ResponseFactory { get; set; }

        /**************************************************************/
        /// <summary>Gets the total handler invocation count.</summary>
        internal int CallCount { get; private set; }

        /**************************************************************/
        /// <summary>Gets captured immutable request snapshots in send order.</summary>
        internal List<RequestSnapshot> Requests { get; } = [];

        /**************************************************************/
        /// <summary>Captures the request and delegates response creation.</summary>
        /// <param name="request">The outgoing request.</param>
        /// <param name="cancellationToken">The operation token.</param>
        /// <returns>The delegate-created response.</returns>
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            #region implementation

            CallCount++;
            var content = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RequestSnapshot(
                request.Method,
                request.RequestUri!,
                request.Headers.Accept.Select(value => value.MediaType!).ToArray(),
                request.Content?.Headers.ContentType?.MediaType,
                content));
            return await ResponseFactory(request, cancellationToken);

            #endregion
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Retains the request values needed after the disposable message leaves the client.</summary>
    /// <param name="Method">The HTTP method.</param>
    /// <param name="Uri">The complete request URI.</param>
    /// <param name="AcceptMediaTypes">The Accept header media types.</param>
    /// <param name="ContentMediaType">The optional content media type.</param>
    /// <param name="Content">The optional request body.</param>
    private sealed record RequestSnapshot(
        HttpMethod Method,
        Uri Uri,
        IReadOnlyList<string> AcceptMediaTypes,
        string? ContentMediaType,
        string? Content);

    #endregion
}
