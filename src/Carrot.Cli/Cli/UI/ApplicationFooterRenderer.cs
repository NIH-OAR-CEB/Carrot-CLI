using Spectre.Console;
using Spectre.Console.Rendering;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>Renders a consistent footer for the interactive application and its result views.</summary>
/// <remarks>
/// The renderer keeps labels and values in separate Spectre objects so dataset names, queries, and
/// service-provided values remain literal. Optional rows allow the same component to serve the main
/// menu and future bounded crawling workflows without duplicating status layout code.
/// </remarks>
/// <seealso cref="ApplicationFooterState"/>
internal sealed class ApplicationFooterRenderer
{
    #region implementation

    private readonly IAnsiConsole _console;

    /**************************************************************/
    /// <summary>Initializes the footer renderer with the injectable Spectre console.</summary>
    /// <param name="console">The console receiving the footer panel.</param>
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
    public void Render(ApplicationFooterState state)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(state);

        var items = new List<(string Label, string Value)>();
        addItem(items, "Context", state.Context);
        addItem(items, "Dataset", state.Dataset);
        addItem(items, "Return Dataset", state.ReturnDataset);

        if (state.ResultPage.HasValue && state.ResultPageCount.HasValue)
        {
            addItem(items, "Data Page", $"{state.ResultPage.Value} of {state.ResultPageCount.Value}");
        }

        if (state.DisplayPage.HasValue && state.DisplayPageCount.HasValue)
        {
            addItem(items, "Display Page", $"{state.DisplayPage.Value} of {state.DisplayPageCount.Value}");
        }

        if (state.CurrentResults.HasValue && state.TotalResults.HasValue)
        {
            addItem(items, "Records", $"{state.CurrentResults.Value} in chunk of {state.TotalResults.Value} total");
        }

        if (state.CanFetchNextResultPage.HasValue)
        {
            addItem(
                items,
                "Next Data Page",
                state.CanFetchNextResultPage.Value
                    ? "Available - Fetch Next Dataset"
                    : "Unavailable - end of result set");
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

        // The blank line separates the summary from records or prompts above it in every caller.
        _console.WriteLine();
        _console.Write(new Panel(table)
            .Header("[bold grey] Summary [/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(Color.Grey)));
        _console.WriteLine();

        #endregion
    }

    /**************************************************************/
    /// <summary>Adds one nonempty literal label/value item to the summary.</summary>
    /// <param name="items">The summary items being populated.</param>
    /// <param name="label">The fixed row label.</param>
    /// <param name="value">The literal row value.</param>
    private static void addItem(List<(string Label, string Value)> items, string label, string? value)
    {
        #region implementation

        // Optional values are omitted so the main-menu summary does not show empty iSearch-only
        // fields, while a result summary still receives every available data element.
        if (!string.IsNullOrWhiteSpace(value))
        {
            items.Add((label, value));
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one escaped summary cell from a fixed label and literal value.</summary>
    /// <param name="item">The label/value pair to render.</param>
    /// <returns>A markup cell whose dynamic value cannot inject terminal markup.</returns>
    private static Markup createItem((string Label, string Value) item)
    {
        #region implementation

        return new Markup($"[bold]{Markup.Escape(item.Label)}:[/] {Markup.Escape(item.Value)}");

        #endregion
    }

    #endregion
}
