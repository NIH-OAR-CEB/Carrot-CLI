using Carrot.Cli.Cli.UI;
using Carrot.Cli.Configuration;
using Carrot.Cli.ISearch.Contracts;
using Spectre.Console.Testing;
using Xunit;

namespace Carrot.Cli.Tests.ISearch;

/**************************************************************/
/// <summary>Verifies the interactive advanced iSearch request draft and review workflow.</summary>
public sealed class AdvancedISearchQueryBuilderTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Ensures the default advanced request is shown as JSON before it can be confirmed.</summary>
    [Fact]
    public async Task BuildAsync_DefaultDraft_PrettyPrintsAndConfirmsRequest()
    {
        #region implementation

        using var console = createConsole();
        selectDraftAction(console, 7);
        console.Input.PushKey(ConsoleKey.Enter);
        var builder = new AdvancedISearchQueryBuilder(console);

        var request = await builder.BuildAsync(
            "live-grants",
            returnType(),
            fields(),
            CancellationToken.None);

        Assert.NotNull(request);
        Assert.Equal("*:*", request.Query);
        Assert.Equal(["grantNumber", "title"], request.Fields);
        Assert.Contains("\"dataset\": \"live-grants\"", console.Output, StringComparison.Ordinal);
        Assert.Contains("\n  \"q\": \"*:*\"", console.Output, StringComparison.Ordinal);
        Assert.Contains("Confirm and Submit", console.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures selected query fields, a numeric filter, and update dates survive confirmation.</summary>
    [Fact]
    public async Task BuildAsync_SelectedFieldsFilterAndDates_CreatesCompleteRequest()
    {
        #region implementation

        using var console = createConsole();

        // Select Query Fields, choose the first live field, and accept the multi-selection prompt.
        selectDraftAction(console, 1);
        console.Input.PushKey(ConsoleKey.Spacebar);
        console.Input.PushKey(ConsoleKey.Enter);

        // Add a filter for the first live field and provide its numeric fiscal-year value.
        selectDraftAction(console, 2);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushTextWithEnter("2024");

        // Set both service update bounds, then review and confirm the resulting package.
        selectDraftAction(console, 5);
        console.Input.PushTextWithEnter("2024-10-01");
        console.Input.PushTextWithEnter("2025-09-30");
        selectDraftAction(console, 7);
        console.Input.PushKey(ConsoleKey.Enter);
        var builder = new AdvancedISearchQueryBuilder(console);

        var request = await builder.BuildAsync(
            "live-grants",
            returnType(),
            fields(),
            CancellationToken.None);

        Assert.NotNull(request);
        Assert.Equal(["fy"], request.QueryFields);
        Assert.Equal(["fy:2024"], request.FilterQueries);
        Assert.Equal("2024-10-01", request.UpdatedAfter);
        Assert.Equal("2025-09-30", request.UpdatedBefore);
        Assert.Contains("fy:2024", console.Output, StringComparison.Ordinal);
        Assert.Contains("updatedAfter", console.Output, StringComparison.Ordinal);
        Assert.Contains("updatedBefore", console.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures string category values with spaces are quoted in field-qualified filters.</summary>
    [Fact]
    public async Task BuildAsync_CategoryPhraseFilter_QuotesValue()
    {
        #region implementation

        using var console = createConsole();
        selectDraftAction(console, 2);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushTextWithEnter("Research Project Grants");
        selectDraftAction(console, 7);
        console.Input.PushKey(ConsoleKey.Enter);
        var builder = new AdvancedISearchQueryBuilder(console);

        var request = await builder.BuildAsync(
            "live-grants",
            returnType(),
            fields(),
            CancellationToken.None);

        Assert.NotNull(request);
        Assert.Equal(["title:\"Research Project Grants\""], request.FilterQueries);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures Escape at the review menu returns no request to the caller.</summary>
    [Fact]
    public async Task BuildAsync_ReviewEscape_CancelsWithoutReturningRequest()
    {
        #region implementation

        using var console = createConsole();
        selectDraftAction(console, 8);
        console.Input.PushKey(ConsoleKey.Escape);
        var builder = new AdvancedISearchQueryBuilder(console);

        var request = await builder.BuildAsync(
            "live-grants",
            returnType(),
            fields(),
            CancellationToken.None);

        Assert.Null(request);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a test console with enough room for prompt rendering.</summary>
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
    /// <summary>Queues keyboard navigation to one action in the draft menu.</summary>
    /// <param name="console">The console receiving the synthetic key input.</param>
    /// <param name="index">The zero-based draft-action index.</param>
    private static void selectDraftAction(TestConsole console, int index)
    {
        #region implementation

        for (var position = 0; position < index; position++)
        {
            // Each menu opens with its highlight on the first action, so the queued arrows are
            // relative to a fresh prompt after the preceding edit completes.
            console.Input.PushKey(ConsoleKey.DownArrow);
        }

        console.Input.PushKey(ConsoleKey.Enter);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates the configured return-field set used by the builder tests.</summary>
    /// <returns>A deterministic return dataset definition.</returns>
    private static SearchReturnTypeDefinition returnType() => new()
    {
        Name = "Grants",
        DefaultFields = ["grantNumber", "title"]
    };

    /**************************************************************/
    /// <summary>Creates live field metadata for query and filter selection.</summary>
    /// <returns>Fields with numeric and string examples.</returns>
    private static IReadOnlyList<SearchField> fields() =>
    [
        new SearchField { Name = "fy", FieldType = "int", DefaultQueryField = true },
        new SearchField { Name = "title", FieldType = "string", DefaultQueryField = true }
    ];

    #endregion
}
