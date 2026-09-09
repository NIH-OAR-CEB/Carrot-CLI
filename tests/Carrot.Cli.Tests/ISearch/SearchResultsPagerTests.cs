using Carrot.Cli.Cli.UI;
using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Carrot.Cli.ISearch;
using Carrot.Cli.ISearch.Contracts;
using Spectre.Console.Testing;
using Xunit;

namespace Carrot.Cli.Tests.ISearch;

/**************************************************************/
/// <summary>Verifies the results pager's separated and colored cardinality summary.</summary>
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
        Assert.Contains("second chunk", console.Output, StringComparison.Ordinal);
        Assert.Contains("Result Page 2 of 2", console.Output, StringComparison.Ordinal);
        Assert.Contains("Next Data Page", console.Output, StringComparison.Ordinal);
        Assert.Contains("Dataset", console.Output, StringComparison.Ordinal);

        #endregion
    }

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
    /// <summary>Provides deterministic iSearch responses for pager tests.</summary>
    private sealed class StubClient : IISearchApiClient
    {
        #region implementation

        /**************************************************************/
        /// <summary>Gets or sets the continuation response.</summary>
        public OperationResult<SearchResponse> NextPage { get; init; } = OperationResult<SearchResponse>.Failure(
            [new OperationMessage { Code = "test.next-page", Message = "not configured", Severity = OperationMessageSeverity.Error }]);

        /**************************************************************/
        /// <summary>Gets the number of continuation calls.</summary>
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
        /// <summary>Records and returns the configured continuation response.</summary>
        /// <param name="request">The stable query request.</param>
        /// <param name="cursor">The current service cursor.</param>
        /// <param name="nextPageNumber">The expected next result page number.</param>
        /// <param name="cancellationToken">The ignored cancellation token.</param>
        public Task<OperationResult<SearchResponse>> SearchNextPageAsync(
            SearchRequest request,
            string cursor,
            int nextPageNumber,
            CancellationToken cancellationToken)
        {
            NextPageCalls++;
            return Task.FromResult(NextPage);
        }

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
