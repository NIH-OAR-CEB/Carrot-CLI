using Carrot.Cli.Cli.UI;
using Carrot.Cli.Configuration;
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
        var pager = new SearchResultsPager(console);

        await pager.ShowAsync(
            new SearchResponse
            {
                Cardinality = new SearchCardinality
                {
                    TotalResults = 289_593,
                    CurrentResults = 100,
                    PageNumber = 1,
                    TotalPages = 2_896
                },
                Results = [System.Text.Json.JsonSerializer.SerializeToElement(new { title = "A result" })]
            },
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
        Assert.Contains("info: request completed\n\n", console.Output, StringComparison.Ordinal);

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
