using Carrot.Cli.Cli.UI;
using Carrot.Cli.ISearch.Contracts;
using Spectre.Console.Testing;
using Xunit;

namespace Carrot.Cli.Tests.ISearch;

/**************************************************************/
/// <summary>Verifies sorting, literal rendering, and bounded navigation for discovered fields.</summary>
public sealed class SearchFieldsPagerTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Ensures the pager displays all seven columns and sorts names ordinally.</summary>
    [Fact]
    public async Task ShowAsync_SortsFieldsAndEscapesServiceText()
    {
        #region implementation

        using var console = createConsole();
        console.Input.PushKey(ConsoleKey.Enter);
        var pager = new SearchFieldsPager(console);

        await pager.ShowAsync(
        [
            new SearchField
            {
                Name = "zeta",
                DisplayName = "[service-label]",
                FieldType = "html",
                DefaultQueryField = false,
                DefaultResultField = true,
                MultiValued = false,
                SearchOnly = true
            },
            new SearchField
            {
                Name = "alpha",
                DisplayName = "Alpha",
                FieldType = "string",
                DefaultQueryField = true,
                DefaultResultField = false,
                MultiValued = true,
                SearchOnly = false
            }
        ], CancellationToken.None);

        Assert.Contains("name", console.Output, StringComparison.Ordinal);
        Assert.Contains("displayName", console.Output, StringComparison.Ordinal);
        Assert.Contains("fieldType", console.Output, StringComparison.Ordinal);
        Assert.Contains("defaultQueryField", console.Output, StringComparison.Ordinal);
        Assert.Contains("defaultResultField", console.Output, StringComparison.Ordinal);
        Assert.Contains("multiValued", console.Output, StringComparison.Ordinal);
        Assert.Contains("searchOnly", console.Output, StringComparison.Ordinal);
        Assert.Contains("[service-label]", console.Output, StringComparison.Ordinal);
        Assert.True(console.Output.IndexOf("alpha", StringComparison.Ordinal) < console.Output.IndexOf("zeta", StringComparison.Ordinal));
        Assert.Contains("True", console.Output, StringComparison.Ordinal);
        Assert.Contains("False", console.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures a long field list uses next and previous page actions within the pager.</summary>
    [Fact]
    public async Task ShowAsync_LongList_PagesForwardAndBack()
    {
        #region implementation

        using var console = createConsole();
        console.Profile.Height = 10;
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        var pager = new SearchFieldsPager(console);
        var fields = Enumerable.Range(1, 6)
            .Select(index => new SearchField { Name = $"field-{index}" })
            .ToArray();

        await pager.ShowAsync(fields, CancellationToken.None);

        Assert.Contains("Page 2 of 2", console.Output, StringComparison.Ordinal);
        Assert.Contains("field-6", console.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates an interactive test console with enough width for the seven-column contract.</summary>
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

    #endregion
}
