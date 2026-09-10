using Spectre.Console;
using Spectre.Console.Rendering;
using System.Globalization;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>Renders a consistent footer for the interactive application and its result views.</summary>
/// <remarks>
/// The renderer keeps labels and values in separate Spectre objects so dataset names, queries, and
/// service-provided values remain literal. Optional rows allow the same component to serve the main
/// menu, one-page result navigation, and the live all-pages iSearch walk without duplicating status
/// layout code. During an active all-pages walk, the progress bar and paging values are highlighted
/// in orange so the loading state is distinguishable from ordinary navigation. The renderer is
/// intentionally stateless; callers provide a fresh snapshot whenever a page or progress value changes.
/// </remarks>
/// <seealso cref="ApplicationFooterState"/>
internal sealed class ApplicationFooterRenderer
{
    #region implementation

    private readonly IAnsiConsole _console;

    /**************************************************************/
    /// <summary>Initializes the footer renderer with the injectable Spectre console.</summary>
    /// <param name="console">The console receiving the footer panel.</param>
    /// <remarks>
    /// The injected console keeps static and live rendering testable and ensures the footer uses the
    /// same terminal capabilities as the surrounding interactive workflow.
    /// </remarks>
    /// <seealso cref="IAnsiConsole"/>
    public ApplicationFooterRenderer(IAnsiConsole console)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(console);
        _console = console;

        #endregion
    }

    /**************************************************************/
    /// <summary>Writes the current application or result-navigation status below interactive content.</summary>
    /// <param name="state">The context and optional paging data to display.</param>
    /// <remarks>
    /// Two blank-line boundaries separate the panel from preceding records and following prompts.
    /// Use <see cref="Create(ApplicationFooterState)"/> when a caller needs to replace a live target
    /// without writing an additional static panel.
    /// </remarks>
    /// <seealso cref="Create(ApplicationFooterState)"/>
    public void Render(ApplicationFooterState state)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(state);

        _console.WriteLine();
        _console.Write(Create(state));
        _console.WriteLine();

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates the shared footer panel without writing it to the console.</summary>
    /// <param name="state">The context and optional paging/progress data to display.</param>
    /// <returns>A renderable panel suitable for static or live console output.</returns>
    /// <remarks>
    /// Optional state values are omitted rather than rendered as placeholders, allowing the same panel
    /// to describe both the main menu and a populated iSearch result session. Dynamic values are escaped
    /// before being placed in Spectre markup, and progress is rendered as a bounded literal bar. The
    /// active all-pages flag controls only the value styling for progress and paging rows.
    /// </remarks>
    /// <seealso cref="ApplicationFooterState"/>
    public Panel Create(ApplicationFooterState state)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(state);

        var items = new List<(string Label, string Value, bool HighlightValue)>();
        addItem(items, "Context", state.Context);
        addItem(items, "Dataset", state.Dataset);
        addItem(items, "Return Dataset", state.ReturnDataset);

        // Service-page metadata is independent from terminal-page metadata; both may be present in an
        // iSearch view, but neither is inferred from the other.
        if (state.ResultPage.HasValue && state.ResultPageCount.HasValue)
        {
            addItem(
                items,
                "Data Page",
                $"{state.ResultPage.Value} of {state.ResultPageCount.Value}",
                highlightValue: state.IsFetchingAllPages);
        }

        // Display navigation is local to materialized terminal lines and must not imply a network fetch.
        if (state.DisplayPage.HasValue && state.DisplayPageCount.HasValue)
        {
            addItem(
                items,
                "Display Page",
                $"{state.DisplayPage.Value} of {state.DisplayPageCount.Value}",
                highlightValue: state.IsFetchingAllPages);
        }

        // CurrentResults describes only the latest service response, while LoadedResults below describes
        // the complete accepted walk; keeping both prevents the summary from hiding partial progress.
        if (state.CurrentResults.HasValue && state.TotalResults.HasValue)
        {
            addItem(items, "Records", $"{state.CurrentResults.Value} in chunk of {state.TotalResults.Value} total");
        }

        // Render progress only when all three values belong to the same snapshot; partial state would be
        // more misleading than omitting the row during a non-iSearch footer render.
        if (state.LoadedResults.HasValue && state.TotalResults.HasValue && state.LoadPercentage.HasValue)
        {
            addItem(
                items,
                "Load Progress",
                createProgressBar(state.LoadedResults.Value, state.TotalResults.Value, state.LoadPercentage.Value),
                highlightValue: state.IsFetchingAllPages);
        }

        // Availability is supplied by the session so the summary cannot offer a stale cursor action.
        if (state.CanFetchNextResultPage.HasValue)
        {
            addItem(
                items,
                "Next Data Page",
                state.CanFetchNextResultPage.Value
                    ? "Available - Fetch Next Dataset"
                    : "Unavailable - end of result set",
                highlightValue: state.IsFetchingAllPages);
        }

        addItem(items, "Guidance", state.Instruction);

        var table = new Table()
            .Border(TableBorder.None)
            .HideHeaders();
        table.AddColumn(new TableColumn(string.Empty));
        table.AddColumn(new TableColumn(string.Empty));

        // Pairing status items keeps the summary compact while retaining every value needed to
        // distinguish a service data page from a terminal display page.
        for (var index = 0; index < items.Count; index += 2)
        {
            IRenderable rightItem = index + 1 < items.Count
                ? createItem(items[index + 1])
                : new Text(string.Empty);
            table.AddRow(createItem(items[index]), rightItem);
        }

        return new Panel(table)
            .Header("[bold grey] Search Summary [/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(Color.Grey));

        #endregion
    }

    /**************************************************************/
    /// <summary>Formats a bounded literal progress bar for the search summary.</summary>
    /// <param name="loadedResults">The number of records accepted by the session.</param>
    /// <param name="totalResults">The stable service total.</param>
    /// <param name="percentage">The calculated loaded percentage.</param>
    /// <returns>A markup-safe text representation with counts and one decimal percentage.</returns>
    private static string createProgressBar(int loadedResults, int totalResults, double percentage)
    {
        #region implementation

        const int barWidth = 24;

        // Clamp both the service-derived percentage and the bar width so malformed input cannot create
        // negative padding, an overlong bar, or a displayed value above 100 percent.
        var boundedPercentage = double.IsFinite(percentage)
            ? Math.Clamp(percentage, 0D, 100D)
            : 0D;
        var filledWidth = (int)Math.Round(
            barWidth * boundedPercentage / 100D,
            MidpointRounding.AwayFromZero);
        var bar = new string('#', filledWidth) + new string('-', barWidth - filledWidth);
        var percentageText = boundedPercentage.ToString("0.0", CultureInfo.InvariantCulture);
        return $"[{bar}] {percentageText}% ({loadedResults}/{Math.Max(0, totalResults)})";

        #endregion
    }

    /**************************************************************/
    /// <summary>Adds one nonempty literal label/value item to the summary.</summary>
    /// <param name="items">The summary items being populated.</param>
    /// <param name="label">The fixed row label.</param>
    /// <param name="value">The literal row value.</param>
    /// <param name="highlightValue">Whether the value should use the active all-pages orange styling.</param>
    private static void addItem(
        List<(string Label, string Value, bool HighlightValue)> items,
        string label,
        string? value,
        bool highlightValue = false)
    {
        #region implementation

        // Optional values are omitted so the main-menu summary does not show empty iSearch-only
        // fields, while a result summary still receives every available data element.
        if (!string.IsNullOrWhiteSpace(value))
        {
            items.Add((label, value, highlightValue));
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one escaped summary cell from a fixed label and literal value.</summary>
    /// <param name="item">The label/value pair and active-loading style to render.</param>
    /// <returns>A markup cell whose dynamic value cannot inject terminal markup.</returns>
    private static Markup createItem((string Label, string Value, bool HighlightValue) item)
    {
        #region implementation

        var escapedValue = Markup.Escape(item.Value);
        var renderedValue = item.HighlightValue
            ? $"[orange1]{escapedValue}[/]"
            : escapedValue;
        return new Markup($"[bold]{Markup.Escape(item.Label)}:[/] {renderedValue}");

        #endregion
    }

    #endregion
}
