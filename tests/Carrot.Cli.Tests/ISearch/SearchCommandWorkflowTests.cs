using System.Text.Json;
using Carrot.Cli.CarrotApi;
using Carrot.Cli.Cli.DependencyInjection;
using Carrot.Cli.Composition;
using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Carrot.Cli.ISearch;
using Carrot.Cli.ISearch.Contracts;
using Carrot.Cli.Reporting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Carrot.Cli.Tests.ISearch;

/**************************************************************/
/// <summary>Verifies named iSearch advanced request construction and bounded retention.</summary>
/// <remarks>Injected service fakes keep these tests independent of credentials, live datasets, and workbook files.</remarks>
/// <seealso cref="SearchCommandWorkflow"/>
public sealed class SearchCommandWorkflowTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Ensures all advanced controls reach the shared search request in CLI order.</summary>
    [Fact]
    public async Task RunAsync_AdvancedOptionsBuildStableRequest()
    {
        #region implementation

        var client = new RecordingSearchClient
        {
            Fields = [new SearchField { Name = "title" }, new SearchField { Name = "abstract" }, new SearchField { Name = "fy" }, new SearchField { Name = "fundingCategory" }]
        };
        var workflow = createWorkflow(client);

        var result = await workflow.RunAsync(new SearchCommandRequest
        {
            Database = "grants",
            ReturnDataset = "Grants",
            Query = "*:*",
            QueryFields = ["title", "abstract"],
            FilterQueries = ["fy:2024", "fundingCategory:\"Research Project Grants\""],
            DefaultOp = "OR",
            Rows = 25,
            UpdatedAfter = "2024-10-01",
            UpdatedBefore = "2025-09-30",
            MaxResults = 25
        }, CancellationToken.None);

        Assert.True(result.Status == OperationStatus.Success, string.Join(" | ", result.Messages.Select(message => message.Message)));
        Assert.NotNull(client.LastSearchRequest);
        Assert.Equal("*:*", client.LastSearchRequest!.Query);
        Assert.Equal(["title", "abstract"], client.LastSearchRequest.QueryFields);
        Assert.Equal(["fy:2024", "fundingCategory:\"Research Project Grants\""], client.LastSearchRequest.FilterQueries);
        Assert.Equal("OR", client.LastSearchRequest.DefaultOp);
        Assert.Equal(25, client.LastSearchRequest.Rows);
        Assert.Equal("2024-10-01", client.LastSearchRequest.UpdatedAfter);
        Assert.Equal("2025-09-30", client.LastSearchRequest.UpdatedBefore);
        Assert.Equal(["nihApplId", "title"], client.LastSearchRequest.Fields);
        Assert.Equal(1, client.GetFieldsCalls);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures field-qualified advanced options use live metadata before searching.</summary>
    [Fact]
    public async Task RunAsync_AdvancedFieldReferences_UsesLiveFields()
    {
        #region implementation

        var client = new RecordingSearchClient
        {
            Fields = [new SearchField { Name = "fy" }, new SearchField { Name = "title" }]
        };
        var workflow = createWorkflow(client);

        var result = await workflow.RunAsync(new SearchCommandRequest
        {
            Database = "grants",
            ReturnDataset = "Grants",
            Query = "*:*",
            QueryFields = ["title"],
            FilterQueries = ["fy:2024"],
            DefaultOp = "AND",
            Rows = 100,
            MaxResults = 100
        }, CancellationToken.None);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(1, client.GetFieldsCalls);
        Assert.Equal(1, client.SearchCalls);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures a bounded page is projected without retaining records beyond the requested cap.</summary>
    [Fact]
    public async Task RunAsync_BoundedCap_RetainsOnlyRequestedPrefix()
    {
        #region implementation

        var client = new RecordingSearchClient
        {
            InitialResponse = createResponse(5, ["one", "two", "three", "four", "five"], "next")
        };
        var workflow = createWorkflow(client);

        var result = await workflow.RunAsync(new SearchCommandRequest
        {
            Database = "grants",
            ReturnDataset = "Grants",
            Query = "pain",
            DefaultOp = "AND",
            Rows = 100,
            MaxResults = 2
        }, CancellationToken.None);

        Assert.Equal(OperationStatus.PartialSuccess, result.Status);
        Assert.Equal(2, result.Value!.Session.WalkedResults.Count);
        Assert.Equal(["one", "two"], result.Value.Session.WalkedResults.Select(item => item.GetProperty("title").GetString()));
        Assert.Equal(0, client.NextPageCalls);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates the production workflow graph with deterministic iSearch and persistence fakes.</summary>
    /// <param name="client">The fake iSearch API client.</param>
    /// <returns>The resolved named workflow.</returns>
    private static ISearchCommandWorkflow createWorkflow(RecordingSearchClient client)
    {
        #region implementation

        var values = new Dictionary<string, string?>
        {
            ["iSearch:apiKey"] = "test-key",
            ["iSearch:contactEmail"] = "test@example.org",
            ["iSearchReturnTypes:Results:Cardinality:TotalResultsFieldName"] = "totalCount",
            ["iSearchReturnTypes:Results:Cardinality:CurrentResultsFieldName"] = "returnedCount",
            ["iSearchReturnTypes:Results:Cardinality:PageNumberFieldName"] = "pageNumber",
            ["iSearchReturnTypes:Results:Cardinality:TotalPagesFieldName"] = "totalPages",
            ["iSearchReturnTypes:Results:Grants:DefaultFields:0"] = "nihApplId",
            ["iSearchReturnTypes:Results:Grants:DefaultFields:1"] = "title"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection().AddCarrotCli(configuration);
        services.AddSingleton<IISearchApiClient>(client);
        services.AddSingleton<ISearchResultsExporter, UnusedSearchExporter>();
        services.AddSingleton<ISearchResultsCategorizer, UnusedCategorizer>();
        services.AddSingleton<ICategorizedISearchResultsExporter, UnusedCategorizedExporter>();

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<ISearchCommandWorkflow>();

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a valid generic response for the bounded-session test.</summary>
    /// <param name="total">The stable service total.</param>
    /// <param name="titles">The returned record titles.</param>
    /// <param name="cursor">The optional continuation cursor.</param>
    /// <returns>A valid iSearch response.</returns>
    private static SearchResponse createResponse(int total, IReadOnlyList<string> titles, string? cursor) => new()
    {
        Cursor = cursor,
        Cardinality = new SearchCardinality
        {
            TotalResults = total,
            CurrentResults = titles.Count,
            PageNumber = 1,
            TotalPages = 1
        },
        Results = titles.Select(title => JsonSerializer.SerializeToElement(new { title })).ToArray()
    };

    /**************************************************************/
    /// <summary>Provides deterministic service responses for workflow tests.</summary>
    private sealed class RecordingSearchClient : IISearchApiClient
    {
        #region implementation

        /**************************************************************/
        /// <summary>Gets the field metadata returned during discovery.</summary>
        public IReadOnlyList<SearchField> Fields { get; init; } = [];

        /**************************************************************/
        /// <summary>Gets the initial response returned by the fake search operation.</summary>
        public SearchResponse InitialResponse { get; init; } = createResponse(1, ["one"], null);

        /**************************************************************/
        /// <summary>Gets the latest search request.</summary>
        public SearchRequest? LastSearchRequest { get; private set; }

        /**************************************************************/
        /// <summary>Gets the number of field discovery calls.</summary>
        public int GetFieldsCalls { get; private set; }

        /**************************************************************/
        /// <summary>Gets the number of initial search calls.</summary>
        public int SearchCalls { get; private set; }

        /**************************************************************/
        /// <summary>Gets the number of continuation calls.</summary>
        public int NextPageCalls { get; private set; }

        /**************************************************************/
        /// <summary>Returns a healthy service response.</summary>
        public Task<OperationResult<SearchHealthResponse>> GetHealthAsync(CancellationToken cancellationToken) =>
            Task.FromResult(OperationResult<SearchHealthResponse>.Success(new SearchHealthResponse { Status = "UP" }));

        /**************************************************************/
        /// <summary>Returns the one live database accepted by the tests.</summary>
        public Task<OperationResult<IReadOnlyList<string>>> GetDatasetsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(OperationResult<IReadOnlyList<string>>.Success(["grants"]));

        /**************************************************************/
        /// <summary>Returns the configured live field metadata.</summary>
        public Task<OperationResult<IReadOnlyList<SearchField>>> GetFieldsAsync(string dataset, CancellationToken cancellationToken)
        {
            GetFieldsCalls++;
            return Task.FromResult(OperationResult<IReadOnlyList<SearchField>>.Success(Fields));
        }

        /**************************************************************/
        /// <summary>Records and returns the deterministic initial page.</summary>
        public Task<OperationResult<SearchResponse>> SearchAsync(SearchRequest request, CancellationToken cancellationToken)
        {
            SearchCalls++;
            LastSearchRequest = request;
            return Task.FromResult(OperationResult<SearchResponse>.Success(InitialResponse));
        }

        /**************************************************************/
        /// <summary>Records a continuation request and returns an unused failure.</summary>
        public Task<OperationResult<SearchResponse>> SearchNextPageAsync(SearchRequest request, string cursor, int nextPageNumber, CancellationToken cancellationToken)
        {
            NextPageCalls++;
            return Task.FromResult(OperationResult<SearchResponse>.Failure(
            [new OperationMessage { Code = "test.unused", Message = "unused", Severity = OperationMessageSeverity.Error }]));
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Prevents workflow tests from performing persistence work.</summary>
    private sealed class UnusedSearchExporter : ISearchResultsExporter
    {
        /**************************************************************/
        /// <summary>Fails if a workflow test unexpectedly requests original output.</summary>
        /// <param name="request">The unexpected export request.</param>
        /// <param name="cancellationToken">The cancellation token for the unexpected operation.</param>
        /// <returns>Never returns because persistence is outside this test's scope.</returns>
        public Task<OperationResult<string>> SaveAsync(SaveISearchResultsRequest request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The test does not request original output.");
    }

    /**************************************************************/
    /// <summary>Prevents workflow tests from performing categorization work.</summary>
    private sealed class UnusedCategorizer : ISearchResultsCategorizer
    {
        /**************************************************************/
        /// <summary>Fails if a workflow test unexpectedly requests categorization.</summary>
        /// <param name="session">The unexpected iSearch result session.</param>
        /// <param name="endpoint">The unexpected Carrot endpoint.</param>
        /// <param name="timeout">The unexpected Carrot timeout.</param>
        /// <param name="cancellationToken">The cancellation token for the unexpected operation.</param>
        /// <returns>Never returns because categorization is outside this test's scope.</returns>
        public Task<OperationResult<CategorizedISearchResultBatch>> CategorizeAsync(SearchResultPageSession session, Uri endpoint, TimeSpan timeout, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The test does not request categorization.");
    }

    /**************************************************************/
    /// <summary>Prevents workflow tests from performing categorized persistence work.</summary>
    private sealed class UnusedCategorizedExporter : ICategorizedISearchResultsExporter
    {
        /**************************************************************/
        /// <summary>Fails if a workflow test unexpectedly requests categorized output.</summary>
        /// <param name="request">The unexpected categorized export request.</param>
        /// <param name="cancellationToken">The cancellation token for the unexpected operation.</param>
        /// <returns>Never returns because persistence is outside this test's scope.</returns>
        public Task<OperationResult<string>> SaveAsync(SaveCategorizedISearchResultsRequest request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The test does not request categorized output.");
    }

    #endregion
}
