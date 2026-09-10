using Carrot.Cli.Cli.UI;
using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Carrot.Cli.ISearch;
using Carrot.Cli.ISearch.Contracts;
using Spectre.Console;
using Spectre.Console.Testing;
using Xunit;

namespace Carrot.Cli.Tests.ISearch;

/**************************************************************/
/// <summary>Verifies the results pager's separated summary and independent navigation actions.</summary>
/// <remarks>
/// The interactive tests use Spectre's test console and queued key input to exercise the actual prompt
/// choices. The all-pages case checks both live progress output and the final ordinary prompt, ensuring
/// the live display does not leak a stale Fetch All Pages action after completion.
/// </remarks>
/// <seealso cref="SearchResultsPager"/>
public sealed class SearchResultsPagerTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Ensures the summary follows a blank separator and precedes the current record page.</summary>
    [Fact]
    public async Task ShowAsync_RendersSeparatedCardinalitySummary()
    {
        #region implementation

        using var console = createConsole();
        console.Write(new Spectre.Console.Text($"info: request completed{Environment.NewLine}"));
        console.Input.PushKey(ConsoleKey.Enter);
        var pager = new SearchResultsPager(console, new ApplicationFooterRenderer(console));

        var response = new SearchResponse
        {
            Cardinality = new SearchCardinality
            {
                TotalResults = 289_593,
                CurrentResults = 100,
                PageNumber = 1,
                TotalPages = 2_896
            },
            Results = [System.Text.Json.JsonSerializer.SerializeToElement(new { title = "A result" })]
        };
        await pager.ShowAsync(
            new SearchResultPageSession(new StubClient(), new SearchRequest
            {
                Dataset = "grants",
                Query = "example",
                Fields = ["title"],
                Rows = 100
            }, response, "Grants"),
            new SearchCardinalityFieldNames
            {
                TotalResultsFieldName = "totalCount",
                CurrentResultsFieldName = "returnedCount",
                PageNumberFieldName = "pageNumber",
                TotalPagesFieldName = "totalPages"
            },
            CancellationToken.None);

        var infoIndex = console.Output.IndexOf("info: request completed", StringComparison.Ordinal);
        var summaryIndex = console.Output.IndexOf("Result Cardinality", StringComparison.Ordinal);
        var resultIndex = console.Output.IndexOf("A result", StringComparison.Ordinal);
        Assert.True(infoIndex >= 0);
        Assert.True(summaryIndex > infoIndex);
        Assert.True(resultIndex > summaryIndex);
        Assert.Contains("totalCount: 289593", console.Output, StringComparison.Ordinal);
        Assert.Contains("returnedCount: 100", console.Output, StringComparison.Ordinal);
        Assert.Contains("pageNumber: 1 of totalPages: 2896", console.Output, StringComparison.Ordinal);
        Assert.Contains("Search Summary", console.Output, StringComparison.Ordinal);
        Assert.Contains("Data Page", console.Output, StringComparison.Ordinal);
        Assert.Contains("Display Page", console.Output, StringComparison.Ordinal);
        Assert.Contains("Records", console.Output, StringComparison.Ordinal);
        Assert.Contains("Return Dataset", console.Output, StringComparison.Ordinal);
        Assert.Contains("info: request completed\n\n", console.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures display navigation does not fetch service data and result fetching replaces the current chunk.</summary>
    [Fact]
    public async Task ShowAsync_DistinguishesDisplayAndResultNavigation()
    {
        #region implementation

        using var console = createConsole();
        console.Profile.Height = 12;
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);
        var client = new StubClient
        {
            NextPage = OperationResult<SearchResponse>.Success(new SearchResponse
            {
                Cardinality = new SearchCardinality
                {
                    TotalResults = 11,
                    CurrentResults = 1,
                    PageNumber = 2,
                    TotalPages = 2
                },
                Results = [System.Text.Json.JsonSerializer.SerializeToElement(new { title = "second chunk" })]
            })
        };
        var response = new SearchResponse
        {
            Cursor = "next cursor",
            Cardinality = new SearchCardinality
            {
                TotalResults = 11,
                CurrentResults = 10,
                PageNumber = 1,
                TotalPages = 2
            },
            Results = Enumerable.Range(0, 10)
                .Select(index => System.Text.Json.JsonSerializer.SerializeToElement(new { title = $"first-{index}" }))
                .ToArray()
        };
        var session = new SearchResultPageSession(client, new SearchRequest
        {
            Dataset = "grants",
            Query = "example",
            Fields = ["title"],
            Rows = 10
        }, response);

        await new SearchResultsPager(console, new ApplicationFooterRenderer(console))
            .ShowAsync(session, createFieldNames(), CancellationToken.None);

        Assert.Equal(1, client.NextPageCalls);
        Assert.Contains("Next Display Page", console.Output, StringComparison.Ordinal);
        Assert.Contains("Fetch Next Result Page", console.Output, StringComparison.Ordinal);
        Assert.Equal("second chunk", session.CurrentPage.Results[0].GetProperty("title").GetString());
        Assert.DoesNotContain("second chunk", console.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures Fetch All Pages updates the summary and leaves the session at the final page.</summary>
    /// <remarks>
    /// The queued three-page response sequence verifies the intermediate 33.3 and 66.7 percent states,
    /// the final 100 percent state, Data Page advancement, and removal of fetch actions after completion.
    /// </remarks>
    [Fact]
    public async Task ShowAsync_FetchAllPagesUpdatesSummaryThroughFinalPage()
    {
        #region implementation

        using var console = createConsole();
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);
        var client = new StubClient
        {
            NextPages = new Queue<OperationResult<SearchResponse>>(
            [
                OperationResult<SearchResponse>.Success(createPage(2, "page-three", "second", 3, 3)),
                OperationResult<SearchResponse>.Success(createPage(3, null, "third", 3, 3))
            ])
        };
        var session = new SearchResultPageSession(
            client,
            new SearchRequest
            {
                Dataset = "grants",
                Query = "example",
                Fields = ["title"],
                Rows = 10
            },
            createPage(1, "page-two", "first", 3, 3));

        await new SearchResultsPager(console, new ApplicationFooterRenderer(console))
            .ShowAsync(session, createFieldNames(), CancellationToken.None);

        Assert.Equal(2, client.NextPageCalls);
        Assert.Equal(3, session.CurrentPage.Cardinality.PageNumber);
        Assert.Equal(3, session.WalkedResults.Count);
        Assert.Contains("Fetch All Pages", console.Output, StringComparison.Ordinal);
        Assert.Contains("> Fetch All Pages", console.Output, StringComparison.Ordinal);
        Assert.Contains("Data Page", console.Output, StringComparison.Ordinal);
        Assert.Contains("33.3%", console.Output, StringComparison.Ordinal);
        Assert.Contains("66.7%", console.Output, StringComparison.Ordinal);
        Assert.Contains("100.0%", console.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Fetch All Pages", console.Output[(console.Output.LastIndexOf("Result Page 3", StringComparison.Ordinal))..], StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures Save invokes the export flow without fetching or replacing the current page.</summary>
    [Fact]
    public async Task ShowAsync_SaveActionUsesRetainedSession()
    {
        #region implementation

        using var console = createConsole();
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Escape);
        var exportFlow = new CapturingExportFlow();
        var page = new SearchResponse
        {
            Cardinality = new SearchCardinality
            {
                TotalResults = 1,
                CurrentResults = 1,
                PageNumber = 1,
                TotalPages = 1
            },
            Results = [System.Text.Json.JsonSerializer.SerializeToElement(new { title = "saved" })]
        };
        var session = new SearchResultPageSession(
            new StubClient(),
            new SearchRequest
            {
                Dataset = "grants",
                Query = "example",
                Fields = ["title"],
                Rows = 100
            },
            page);

        await new SearchResultsPager(console, new ApplicationFooterRenderer(console), exportFlow)
            .ShowAsync(session, createFieldNames(), CancellationToken.None);

        Assert.Same(session, exportFlow.Session);
        Assert.Contains("Save iSearch Results to Excel", console.Output, StringComparison.Ordinal);
        Assert.Equal("saved", session.WalkedResults[0].GetProperty("title").GetString());

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures categorization receives the same loaded session without fetching more records.</summary>
    [Fact]
    public async Task ShowAsync_CategorizeActionUsesRetainedSession()
    {
        #region implementation

        // Arrange
        using var console = createConsole();
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Escape);
        var categorizationFlow = new CapturingCategorizationFlow();
        var session = new SearchResultPageSession(
            new StubClient(),
            new SearchRequest
            {
                Dataset = "grants",
                Query = "example",
                Fields = ["nihApplId", "title", "abstract", "specificAims"],
                Rows = 100
            },
            new SearchResponse
            {
                Cardinality = new SearchCardinality
                {
                    TotalResults = 1,
                    CurrentResults = 1,
                    PageNumber = 1,
                    TotalPages = 1
                },
                Results = [System.Text.Json.JsonSerializer.SerializeToElement(new { title = "loaded" })]
            });

        // Act
        await new SearchResultsPager(
                console,
                new ApplicationFooterRenderer(console),
                categorizationFlow: categorizationFlow,
                categorizedResultsPager: new StubCategorizedResultsPager())
            .ShowAsync(session, createFieldNames(), CancellationToken.None);

        // Assert
        Assert.Same(session, categorizationFlow.Session);
        Assert.Contains("Categorize iSearch Results", console.Output, StringComparison.Ordinal);
        Assert.Single(session.WalkedResults);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures an export flow with its own prompt runs after LiveDisplay releases the console.</summary>
    [Fact]
    public async Task ShowAsync_SaveActionReleasesLiveDisplayBeforeExportPrompt()
    {
        #region implementation

        using var console = createConsole();
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushTextWithEnter("results.xlsx");
        console.Input.PushKey(ConsoleKey.Escape);
        var exportFlow = new PromptingExportFlow(console);
        var session = new SearchResultPageSession(
            new StubClient(),
            new SearchRequest
            {
                Dataset = "grants",
                Query = "example",
                Fields = ["title"],
                Rows = 100
            },
            new SearchResponse
            {
                Cardinality = new SearchCardinality
                {
                    TotalResults = 1,
                    CurrentResults = 1,
                    PageNumber = 1,
                    TotalPages = 1
                },
                Results = [System.Text.Json.JsonSerializer.SerializeToElement(new { title = "saved" })]
            });

        await new SearchResultsPager(console, new ApplicationFooterRenderer(console), exportFlow)
            .ShowAsync(session, createFieldNames(), CancellationToken.None);

        Assert.Equal(1, exportFlow.CallCount);
        Assert.Equal("results.xlsx", exportFlow.Path);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a one-record response page for all-pages pager tests.</summary>
    /// <param name="pageNumber">The service data-page number.</param>
    /// <param name="cursor">The optional continuation cursor.</param>
    /// <param name="title">The synthetic record title.</param>
    /// <param name="totalResults">The stable total result count.</param>
    /// <param name="totalPages">The total service-page count.</param>
    /// <returns>A synthetic service response.</returns>
    /// <remarks>The cursor and cardinality values deliberately form a valid sequential walk.</remarks>
    private static SearchResponse createPage(
        int pageNumber,
        string? cursor,
        string title,
        int totalResults,
        int totalPages) => new()
        {
            Cursor = cursor,
            Cardinality = new SearchCardinality
            {
                TotalResults = totalResults,
                CurrentResults = 1,
                PageNumber = pageNumber,
                TotalPages = totalPages
            },
            Results = [System.Text.Json.JsonSerializer.SerializeToElement(new { title })]
        };

    /**************************************************************/
    /// <summary>Creates the shared cardinality labels used by pager tests.</summary>
    /// <returns>Valid report labels.</returns>
    private static SearchCardinalityFieldNames createFieldNames() => new()
    {
        TotalResultsFieldName = "totalCount",
        CurrentResultsFieldName = "returnedCount",
        PageNumberFieldName = "pageNumber",
        TotalPagesFieldName = "totalPages"
    };

    /**************************************************************/
    /// <summary>Captures the loaded session passed to the categorization flow.</summary>
    private sealed class CapturingCategorizationFlow : ISearchResultsCategorizationFlow
    {
        #region implementation

        /**************************************************************/
        /// <summary>Gets the session supplied by the pager.</summary>
        public SearchResultPageSession? Session { get; private set; }

        /**************************************************************/
        /// <summary>Captures the session and returns an expected test failure.</summary>
        /// <param name="session">The loaded iSearch session.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A failure so the test does not enter categorized paging.</returns>
        public Task<OperationResult<CategorizedISearchResultBatch>> RunAsync(
            SearchResultPageSession session,
            CancellationToken cancellationToken)
        {
            Session = session;
            return Task.FromResult(OperationResult<CategorizedISearchResultBatch>.Failure(
                [new OperationMessage
                {
                    Code = "test.categorization",
                    Message = "Expected test failure.",
                    Severity = OperationMessageSeverity.Error
                }]));
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Provides a categorized pager that should not be reached after a failed categorization.</summary>
    private sealed class StubCategorizedResultsPager : ICategorizedISearchResultsPager
    {
        #region implementation

        /**************************************************************/
        /// <summary>Completes without rendering because the categorization flow failed.</summary>
        /// <param name="batch">The unused categorized batch.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A completed display task.</returns>
        public Task ShowAsync(CategorizedISearchResultBatch batch, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        #endregion
    }

    /**************************************************************/
    /// <summary>Provides deterministic iSearch responses for pager tests.</summary>
    private sealed class StubClient : IISearchApiClient
    {
        #region implementation

        /**************************************************************/
        /// <summary>Gets or sets the fallback continuation response.</summary>
        /// <remarks>The fallback is used by tests that exercise one-page behavior.</remarks>
        public OperationResult<SearchResponse> NextPage { get; init; } = OperationResult<SearchResponse>.Failure(
            [new OperationMessage { Code = "test.next-page", Message = "not configured", Severity = OperationMessageSeverity.Error }]);

        /**************************************************************/
        /// <summary>Gets or sets queued continuation responses for all-pages tests.</summary>
        /// <remarks>Each call consumes one response, preserving the order that a real cursor walk would observe.</remarks>
        public Queue<OperationResult<SearchResponse>> NextPages { get; init; } = [];

        /**************************************************************/
        /// <summary>Gets the number of continuation calls.</summary>
        /// <remarks>The assertion uses this value to distinguish local display navigation from data fetching.</remarks>
        public int NextPageCalls { get; private set; }

        /**************************************************************/
        /// <summary>Returns an unused health failure.</summary>
        /// <param name="cancellationToken">The ignored cancellation token.</param>
        public Task<OperationResult<SearchHealthResponse>> GetHealthAsync(CancellationToken cancellationToken) =>
            Task.FromResult(OperationResult<SearchHealthResponse>.Failure(
                [new OperationMessage { Code = "test.unused", Message = "unused", Severity = OperationMessageSeverity.Error }]));

        /**************************************************************/
        /// <summary>Returns an unused dataset failure.</summary>
        /// <param name="cancellationToken">The ignored cancellation token.</param>
        public Task<OperationResult<IReadOnlyList<string>>> GetDatasetsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(OperationResult<IReadOnlyList<string>>.Failure(
                [new OperationMessage { Code = "test.unused", Message = "unused", Severity = OperationMessageSeverity.Error }]));

        /**************************************************************/
        /// <summary>Returns an unused field failure.</summary>
        /// <param name="dataset">The unused dataset.</param>
        /// <param name="cancellationToken">The ignored cancellation token.</param>
        public Task<OperationResult<IReadOnlyList<SearchField>>> GetFieldsAsync(string dataset, CancellationToken cancellationToken) =>
            Task.FromResult(OperationResult<IReadOnlyList<SearchField>>.Failure(
                [new OperationMessage { Code = "test.unused", Message = "unused", Severity = OperationMessageSeverity.Error }]));

        /**************************************************************/
        /// <summary>Returns an unused initial-search failure.</summary>
        /// <param name="request">The unused search request.</param>
        /// <param name="cancellationToken">The ignored cancellation token.</param>
        public Task<OperationResult<SearchResponse>> SearchAsync(SearchRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(OperationResult<SearchResponse>.Failure(
                [new OperationMessage { Code = "test.unused", Message = "unused", Severity = OperationMessageSeverity.Error }]));

        /**************************************************************/
        /// <summary>Records and returns the next deterministic continuation response.</summary>
        /// <param name="request">The stable query request.</param>
        /// <param name="cursor">The current service cursor.</param>
        /// <param name="nextPageNumber">The expected next result page number.</param>
        /// <param name="cancellationToken">The ignored cancellation token.</param>
        /// <returns>The next queued response, or the configured fallback when the queue is empty.</returns>
        /// <remarks>The fake intentionally performs no waiting so pager state assertions stay independent of service timing.</remarks>
        public Task<OperationResult<SearchResponse>> SearchNextPageAsync(
            SearchRequest request,
            string cursor,
            int nextPageNumber,
            CancellationToken cancellationToken)
        {
            NextPageCalls++;
            return Task.FromResult(NextPages.Count > 0 ? NextPages.Dequeue() : NextPage);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Captures the session supplied by the pager's save action.</summary>
    /// <remarks>This test double proves Save receives retained state without coupling the pager tests to file I/O.</remarks>
    private sealed class CapturingExportFlow : ISearchResultsExportFlow
    {
        #region implementation

        /**************************************************************/
        /// <summary>Gets the session received by the save action.</summary>
        /// <remarks>The value remains null until the pager invokes the export flow.</remarks>
        public SearchResultPageSession? Session { get; private set; }

        /**************************************************************/
        /// <summary>Captures the retained session without performing file I/O.</summary>
        /// <param name="session">The session selected for export.</param>
        /// <param name="cancellationToken">The ignored cancellation token.</param>
        /// <returns>A completed interaction task.</returns>
        /// <remarks>The captured reference is the same session instance shown by the pager.</remarks>
        public Task RunAsync(SearchResultPageSession session, CancellationToken cancellationToken)
        {
            Session = session;
            return Task.CompletedTask;
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Exercises an export prompt while proving LiveDisplay has already released the console.</summary>
    private sealed class PromptingExportFlow : ISearchResultsExportFlow
    {
        #region implementation

        private readonly IAnsiConsole _console;

        /**************************************************************/
        /// <summary>Initializes the prompt-capable export test double.</summary>
        /// <param name="console">The test console receiving the export prompt.</param>
        public PromptingExportFlow(IAnsiConsole console)
        {
            _console = console;
        }

        /**************************************************************/
        /// <summary>Prompts for a path and records the result without performing file I/O.</summary>
        /// <param name="session">The retained result session supplied by the pager.</param>
        /// <param name="cancellationToken">The token controlling the prompt.</param>
        /// <returns>A task representing the simulated export interaction.</returns>
        public async Task RunAsync(SearchResultPageSession session, CancellationToken cancellationToken)
        {
            Path = await new TextPrompt<string>("Export path:")
                .ShowAsync(_console, cancellationToken)
                .ConfigureAwait(false);
            CallCount++;
        }

        /**************************************************************/
        /// <summary>Gets the number of simulated export calls.</summary>
        public int CallCount { get; private set; }

        /**************************************************************/
        /// <summary>Gets the path entered into the simulated export prompt.</summary>
        public string? Path { get; private set; }

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates an interactive test console large enough for the footer and navigation prompt.</summary>
    /// <returns>The configured test console.</returns>
    private static TestConsole createConsole()
    {
        #region implementation

        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Profile.Width = 160;
        console.Profile.Height = 30;
        return console;

        #endregion
    }

    #endregion
}
