using System.Text.Json;
using Carrot.Cli.Cli.UI;
using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Carrot.Cli.ISearch;
using Carrot.Cli.ISearch.Contracts;
using Microsoft.Extensions.Configuration;
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
                Cardinality = new SearchCardinality
                {
                    CurrentResults = 1,
                    TotalResults = 1,
                    PageNumber = 1,
                    TotalPages = 1
                },
                Results = [JsonSerializer.SerializeToElement(new { title = "A result" })]
            })
        };
        // Select Database, choose the only live dataset, select the configured return dataset,
        // submit the query, enter the query, then back out twice.
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushTextWithEnter("vaccine research");
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        var flow = createFlow(console, client, validOptions());

        await flow.RunAsync(CancellationToken.None);

        Assert.Equal("live-grants", client.LastSearch!.Dataset);
        Assert.Equal("vaccine research", client.LastSearch.Query);
        Assert.Equal(["grantNumber", "title"], client.LastSearch.Fields);
        Assert.Equal("AND", client.LastSearch.DefaultOp);
        Assert.Equal(100, client.LastSearch.Rows);
        Assert.Contains("Select Return Dataset", console.Output, StringComparison.Ordinal);
        Assert.Contains("Summaries", console.Output, StringComparison.Ordinal);
        Assert.Contains("Result Cardinality", console.Output, StringComparison.Ordinal);
        Assert.Contains("returnedCount: 1", console.Output, StringComparison.Ordinal);
        Assert.Contains("A result", console.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures the advanced menu action discovers fields, reviews JSON, and submits the confirmed request.</summary>
    [Fact]
    public async Task RunAsync_BuildAdvancedQuery_SubmitsReviewedRequest()
    {
        #region implementation

        using var console = createConsole();
        var client = createHealthyClient();
        client.Fields = OperationResult<IReadOnlyList<SearchField>>.Success(
        [
            new SearchField { Name = "fy", FieldType = "int", DefaultQueryField = true }
        ]);

        // Select the live database and configured return set, choose Build Advanced Query, review
        // the default match-all package, confirm it, then leave both result and dataset menus.
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);
        for (var position = 0; position < 4; position++)
        {
            console.Input.PushKey(ConsoleKey.DownArrow);
        }

        console.Input.PushKey(ConsoleKey.Enter);
        for (var position = 0; position < 7; position++)
        {
            console.Input.PushKey(ConsoleKey.DownArrow);
        }

        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Escape);
        console.Input.PushKey(ConsoleKey.Escape);
        var flow = createFlow(console, client, validOptions());

        await flow.RunAsync(CancellationToken.None);

        Assert.Equal(1, client.FieldsCalls);
        Assert.Equal("live grants", client.LastFieldsDataset);
        Assert.Equal("*:*", client.LastSearch!.Query);
        Assert.Null(client.LastSearch.FilterQueries);
        Assert.Contains("Build Advanced Query", console.Output, StringComparison.Ordinal);
        Assert.Contains("JSON package to submit", console.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures fetching the next service page keeps the selected return dataset unchanged.</summary>
    [Fact]
    public async Task RunAsync_FetchesNextResultPage_WithoutChangingReturnDataset()
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
                Cursor = "next-token",
                Cardinality = new SearchCardinality
                {
                    CurrentResults = 1,
                    TotalResults = 2,
                    PageNumber = 1,
                    TotalPages = 2
                },
                Results = [JsonSerializer.SerializeToElement(new { title = "first chunk" })]
            }),
            NextPage = OperationResult<SearchResponse>.Success(new SearchResponse
            {
                Cardinality = new SearchCardinality
                {
                    CurrentResults = 1,
                    TotalResults = 2,
                    PageNumber = 2,
                    TotalPages = 2
                },
                Results = [JsonSerializer.SerializeToElement(new { title = "second chunk" })]
            })
        };

        // Select the live database, select Grants, submit one query, fetch the next service page,
        // then return to the dataset menu without reopening the return-dataset picker.
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushTextWithEnter("vaccine research");
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        var flow = createFlow(console, client, validOptions());

        await flow.RunAsync(CancellationToken.None);

        Assert.Equal(1, client.NextPageCalls);
        Assert.Equal("next-token", client.LastCursor);
        Assert.Equal(2, client.LastNextPageNumber);
        Assert.Equal("live-grants", client.LastSearch!.Dataset);
        Assert.Equal(["grantNumber", "title"], client.LastSearch.Fields);
        Assert.Contains("Fetch Next Result Page", console.Output, StringComparison.Ordinal);
        Assert.Contains("Result Page 2 of 2", console.Output, StringComparison.Ordinal);
        Assert.Contains("second chunk", console.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures a query cannot be submitted until a configured return dataset is selected.</summary>
    [Fact]
    public async Task RunAsync_WithoutReturnDataset_DoesNotSubmitQuery()
    {
        #region implementation

        using var console = createConsole();
        var client = createHealthyClient();
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        var flow = createFlow(console, client, validOptions());

        await flow.RunAsync(CancellationToken.None);

        Assert.Null(client.LastSearch);
        Assert.DoesNotContain("Query [", console.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures field discovery renders sorted metadata and retains the dataset for querying.</summary>
    [Fact]
    public async Task RunAsync_ViewFields_RendersSortedMetadataAndRetainsDataset()
    {
        #region implementation

        using var console = createConsole();
        var client = createHealthyClient();
        client.Fields = OperationResult<IReadOnlyList<SearchField>>.Success(
        [
            new SearchField
            {
                Name = "zeta",
                DisplayName = "Zeta",
                FieldType = "string",
                DefaultQueryField = false,
                DefaultResultField = true,
                MultiValued = null,
                SearchOnly = false
            },
            new SearchField
            {
                Name = "alpha",
                DisplayName = "Alpha",
                FieldType = "score",
                DefaultQueryField = true,
                DefaultResultField = false,
                MultiValued = true,
                SearchOnly = true
            }
        ]);

        // Select the dataset, view fields, return to the dataset menu, select a return dataset,
        // submit a query, then leave both menus.
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushTextWithEnter("schema check");
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        var flow = createFlow(console, client, validOptions());

        await flow.RunAsync(CancellationToken.None);

        Assert.Equal(1, client.FieldsCalls);
        Assert.Equal("live grants", client.LastFieldsDataset);
        Assert.Equal("live grants", client.LastSearch!.Dataset);
        Assert.Equal(["grantNumber", "title"], client.LastSearch.Fields);
        Assert.Contains("name", console.Output, StringComparison.Ordinal);
        Assert.Contains("displayName", console.Output, StringComparison.Ordinal);
        Assert.Contains("defaultQueryField", console.Output, StringComparison.Ordinal);
        Assert.Contains("alpha", console.Output, StringComparison.Ordinal);
        Assert.Contains("zeta", console.Output, StringComparison.Ordinal);
        Assert.True(console.Output.IndexOf("alpha", StringComparison.Ordinal) < console.Output.IndexOf("zeta", StringComparison.Ordinal));

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures field discovery cannot be selected before a dataset is selected.</summary>
    [Fact]
    public async Task RunAsync_BacksOutBeforeSelection_DoesNotRequestFields()
    {
        #region implementation

        using var console = createConsole();
        var client = createHealthyClient();
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        var flow = createFlow(console, client, validOptions());

        await flow.RunAsync(CancellationToken.None);

        Assert.Equal(0, client.FieldsCalls);
        Assert.DoesNotContain("View Fields", console.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures an empty field response reports no fields and still returns to the dataset menu.</summary>
    [Fact]
    public async Task RunAsync_EmptyFields_ReturnsToDatasetMenu()
    {
        #region implementation

        using var console = createConsole();
        var client = createHealthyClient();
        client.Fields = OperationResult<IReadOnlyList<SearchField>>.Success([]);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Escape);
        var flow = createFlow(console, client, validOptions());

        await flow.RunAsync(CancellationToken.None);

        Assert.Contains("no fields", console.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, client.FieldsCalls);

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
    /// <summary>Ensures malformed return-dataset configuration stops before health discovery.</summary>
    [Fact]
    public async Task RunAsync_MissingReturnDatasetConfiguration_DoesNotCallHealth()
    {
        #region implementation

        using var console = createConsole();
        var client = new FakeClient();
        var emptyCatalog = new SearchReturnTypeCatalog(new ConfigurationBuilder().Build());
        var flow = createFlow(console, client, validOptions(), emptyCatalog);

        await flow.RunAsync(CancellationToken.None);

        Assert.Equal(0, client.HealthCalls);
        Assert.Contains("return datasets", console.Output, StringComparison.OrdinalIgnoreCase);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates the flow with a deterministic fake API and console pager.</summary>
    /// <param name="console">The test console.</param>
    /// <param name="client">The fake API client.</param>
    /// <param name="options">The iSearch settings.</param>
    /// <returns>The flow under test.</returns>
    private static InteractiveISearchFlow createFlow(
        TestConsole console,
        FakeClient client,
        ISearchOptions options,
        SearchReturnTypeCatalog? returnTypeCatalog = null)
    {
        #region implementation

        return new InteractiveISearchFlow(
            console,
            Options.Create(options),
            new ISearchOptionsValidator(),
            returnTypeCatalog ?? createReturnTypeCatalog(),
            client,
            new SearchResultsPager(console, new ApplicationFooterRenderer(console)),
            new SearchFieldsPager(console),
            new AdvancedISearchQueryBuilder(console));

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
        console.Profile.Width = 240;
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
    /// <summary>Creates the configured return dataset used by interactive flow tests.</summary>
    /// <returns>A catalog containing deterministic synthetic result fields.</returns>
    private static SearchReturnTypeCatalog createReturnTypeCatalog()
    {
        #region implementation

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["iSearchReturnTypes:Results:Cardinality:TotalResultsFieldName"] = "totalCount",
                ["iSearchReturnTypes:Results:Cardinality:CurrentResultsFieldName"] = "returnedCount",
                ["iSearchReturnTypes:Results:Cardinality:PageNumberFieldName"] = "pageNumber",
                ["iSearchReturnTypes:Results:Cardinality:TotalPagesFieldName"] = "totalPages",
                ["iSearchReturnTypes:Results:Grants:DefaultFields:0"] = "grantNumber",
                ["iSearchReturnTypes:Results:Grants:DefaultFields:1"] = "title",
                ["iSearchReturnTypes:Results:Summaries:DefaultFields:0"] = "id"
            })
            .Build();
        return new SearchReturnTypeCatalog(configuration);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a healthy fake client with one live dataset.</summary>
    /// <returns>The configured fake iSearch client.</returns>
    private static FakeClient createHealthyClient() => new()
    {
        Health = OperationResult<SearchHealthResponse>.Success(new SearchHealthResponse
        {
            Status = "UP",
            Payload = JsonSerializer.SerializeToElement(new { status = "UP" })
        }),
        Datasets = OperationResult<IReadOnlyList<string>>.Success(["live grants"]),
        Search = OperationResult<SearchResponse>.Success(new SearchResponse
        {
            Cardinality = new SearchCardinality
            {
                CurrentResults = 0,
                TotalResults = 0,
                PageNumber = 0,
                TotalPages = 0
            },
            Results = []
        })
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
        /// <summary>Gets or sets the configured continuation-page result.</summary>
        public OperationResult<SearchResponse> NextPage { get; init; } = OperationResult<SearchResponse>.Failure(
            [new OperationMessage { Code = "test.next-page", Message = "Next page was not configured.", Severity = OperationMessageSeverity.Error }]);

        /**************************************************************/
        /// <summary>Gets or sets the field result.</summary>
        public OperationResult<IReadOnlyList<SearchField>> Fields { get; set; } = OperationResult<IReadOnlyList<SearchField>>.Success([]);

        /**************************************************************/
        /// <summary>Gets the number of health calls.</summary>
        public int HealthCalls { get; private set; }

        /**************************************************************/
        /// <summary>Gets the number of dataset calls.</summary>
        public int DatasetCalls { get; private set; }

        /**************************************************************/
        /// <summary>Gets the number of field calls.</summary>
        public int FieldsCalls { get; private set; }

        /**************************************************************/
        /// <summary>Gets the dataset used for the last field call.</summary>
        public string? LastFieldsDataset { get; private set; }

        /**************************************************************/
        /// <summary>Gets the last submitted request.</summary>
        public SearchRequest? LastSearch { get; private set; }

        /**************************************************************/
        /// <summary>Gets the number of continuation-page calls.</summary>
        public int NextPageCalls { get; private set; }

        /**************************************************************/
        /// <summary>Gets the cursor used by the last continuation-page call.</summary>
        public string? LastCursor { get; private set; }

        /**************************************************************/
        /// <summary>Gets the page number used by the last continuation-page call.</summary>
        public int LastNextPageNumber { get; private set; }

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
        /// <summary>Records and returns the configured field result.</summary>
        /// <param name="dataset">The selected dataset.</param>
        /// <param name="cancellationToken">The ignored test cancellation token.</param>
        public Task<OperationResult<IReadOnlyList<SearchField>>> GetFieldsAsync(string dataset, CancellationToken cancellationToken)
        {
            FieldsCalls++;
            LastFieldsDataset = dataset;
            return Task.FromResult(Fields);
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

        /**************************************************************/
        /// <summary>Records and returns the configured continuation-page result.</summary>
        /// <param name="request">The stable query context.</param>
        /// <param name="cursor">The cursor from the current response.</param>
        /// <param name="nextPageNumber">The expected next service page number.</param>
        /// <param name="cancellationToken">The ignored test cancellation token.</param>
        public Task<OperationResult<SearchResponse>> SearchNextPageAsync(
            SearchRequest request,
            string cursor,
            int nextPageNumber,
            CancellationToken cancellationToken)
        {
            NextPageCalls++;
            LastSearch = request;
            LastCursor = cursor;
            LastNextPageNumber = nextPageNumber;
            return Task.FromResult(NextPage);
        }

        #endregion
    }

    #endregion
}
