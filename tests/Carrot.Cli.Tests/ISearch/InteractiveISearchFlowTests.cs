using System.Text.Json;
using Carrot.Cli.Cli.UI;
using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Carrot.Cli.ISearch;
using Carrot.Cli.ISearch.Contracts;
using Microsoft.Extensions.Options;
using Spectre.Console.Testing;
using Xunit;

namespace Carrot.Cli.Tests.ISearch;

/**************************************************************/
/// <summary>Verifies iSearch interactive health gating, live selection, query submission, and rendering.</summary>
public sealed class InteractiveISearchFlowTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Ensures a negative health status stops before dataset discovery.</summary>
    [Fact]
    public async Task RunAsync_NegativeHealth_DoesNotDiscoverDatasets()
    {
        #region implementation

        using var console = createConsole();
        var client = new FakeClient
        {
            Health = OperationResult<SearchHealthResponse>.Success(new SearchHealthResponse
            {
                Status = "DOWN",
                Payload = JsonSerializer.SerializeToElement(new { status = "DOWN" })
            })
        };
        var flow = createFlow(console, client, new ISearchOptions
        {
            ApiKey = "synthetic-test-key",
            ContactEmail = "operator@example.org"
        });

        await flow.RunAsync(CancellationToken.None);

        Assert.Equal(0, client.DatasetCalls);
        Assert.Contains("status", console.Output, StringComparison.Ordinal);
        Assert.Contains("not available", console.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures live dataset selection reaches a bounded search and renders returned records.</summary>
    [Fact]
    public async Task RunAsync_SelectsLiveDatasetAndSubmitsQuery()
    {
        #region implementation

        using var console = createConsole();
        var client = new FakeClient
        {
            Health = OperationResult<SearchHealthResponse>.Success(new SearchHealthResponse
            {
                Status = "UP",
                Payload = JsonSerializer.SerializeToElement(new { status = "UP" })
            }),
            Datasets = OperationResult<IReadOnlyList<string>>.Success(["live-grants"]),
            Search = OperationResult<SearchResponse>.Success(new SearchResponse
            {
                ReturnedCount = 1,
                TotalCount = 1,
                Results = [JsonSerializer.SerializeToElement(new { title = "A result" })]
            })
        };
        // Select Database, choose the only live dataset, Submit Query, enter the query, then back out twice.
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushTextWithEnter("vaccine research");
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        var flow = createFlow(console, client, validOptions());

        await flow.RunAsync(CancellationToken.None);

        Assert.Equal("live-grants", client.LastSearch!.Dataset);
        Assert.Equal("vaccine research", client.LastSearch.Query);
        Assert.Equal("AND", client.LastSearch.DefaultOp);
        Assert.Equal(100, client.LastSearch.Rows);
        Assert.Contains("returned 1 of 1", console.Output, StringComparison.Ordinal);
        Assert.Contains("A result", console.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures missing configuration returns without calling health.</summary>
    [Fact]
    public async Task RunAsync_MissingConfiguration_DoesNotCallHealth()
    {
        #region implementation

        using var console = createConsole();
        var client = new FakeClient();
        var flow = createFlow(console, client, new ISearchOptions { ContactEmail = "operator@example.org" });

        await flow.RunAsync(CancellationToken.None);

        Assert.Equal(0, client.HealthCalls);
        Assert.Contains("API key", console.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates the flow with a deterministic fake API and console pager.</summary>
    /// <param name="console">The test console.</param>
    /// <param name="client">The fake API client.</param>
    /// <param name="options">The iSearch settings.</param>
    /// <returns>The flow under test.</returns>
    private static InteractiveISearchFlow createFlow(TestConsole console, FakeClient client, ISearchOptions options)
    {
        #region implementation

        return new InteractiveISearchFlow(
            console,
            Options.Create(options),
            new ISearchOptionsValidator(),
            client,
            new SearchResultsPager(console));

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates an interactive test console with enough height for one results page.</summary>
    /// <returns>The configured console.</returns>
    private static TestConsole createConsole()
    {
        #region implementation

        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Profile.Height = 200;
        return console;

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates valid synthetic options with zero pacing for deterministic tests.</summary>
    /// <returns>Safe test options.</returns>
    private static ISearchOptions validOptions() => new()
    {
        ApiKey = "synthetic-test-key",
        ContactEmail = "operator@example.org",
        MinimumRequestIntervalMilliseconds = 0
    };

    /**************************************************************/
    /// <summary>Supplies deterministic iSearch operation results while recording calls.</summary>
    private sealed class FakeClient : IISearchApiClient
    {
        #region implementation

        /**************************************************************/
        /// <summary>Gets or sets the health result.</summary>
        public OperationResult<SearchHealthResponse> Health { get; init; } = OperationResult<SearchHealthResponse>.Failure(
            [new OperationMessage { Code = "test.health", Message = "Health was not configured.", Severity = OperationMessageSeverity.Error }]);

        /**************************************************************/
        /// <summary>Gets or sets the dataset result.</summary>
        public OperationResult<IReadOnlyList<string>> Datasets { get; init; } = OperationResult<IReadOnlyList<string>>.Success([]);

        /**************************************************************/
        /// <summary>Gets or sets the search result.</summary>
        public OperationResult<SearchResponse> Search { get; init; } = OperationResult<SearchResponse>.Failure(
            [new OperationMessage { Code = "test.search", Message = "Search was not configured.", Severity = OperationMessageSeverity.Error }]);

        /**************************************************************/
        /// <summary>Gets the number of health calls.</summary>
        public int HealthCalls { get; private set; }

        /**************************************************************/
        /// <summary>Gets the number of dataset calls.</summary>
        public int DatasetCalls { get; private set; }

        /**************************************************************/
        /// <summary>Gets the last submitted request.</summary>
        public SearchRequest? LastSearch { get; private set; }

        /**************************************************************/
        /// <summary>Returns the configured health result.</summary>
        /// <param name="cancellationToken">The ignored test cancellation token.</param>
        public Task<OperationResult<SearchHealthResponse>> GetHealthAsync(CancellationToken cancellationToken)
        {
            HealthCalls++;
            return Task.FromResult(Health);
        }

        /**************************************************************/
        /// <summary>Returns the configured datasets result.</summary>
        /// <param name="cancellationToken">The ignored test cancellation token.</param>
        public Task<OperationResult<IReadOnlyList<string>>> GetDatasetsAsync(CancellationToken cancellationToken)
        {
            DatasetCalls++;
            return Task.FromResult(Datasets);
        }

        /**************************************************************/
        /// <summary>Records and returns the configured search result.</summary>
        /// <param name="request">The submitted query request.</param>
        /// <param name="cancellationToken">The ignored test cancellation token.</param>
        public Task<OperationResult<SearchResponse>> SearchAsync(SearchRequest request, CancellationToken cancellationToken)
        {
            LastSearch = request;
            return Task.FromResult(Search);
        }

        #endregion
    }

    #endregion
}
