using Carrot.Cli.ISearch.Contracts;
using Spectre.Console;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>Renders discovered iSearch field metadata in terminal-sized pages.</summary>
/// <remarks>
/// Rows are sorted by the service-owned field name and rendered through escaped table cells so
/// arbitrary labels and type strings cannot be interpreted as Spectre.Console markup.
/// </remarks>
/// <seealso cref="ISearchFieldsPager"/>
/// <seealso cref="SearchField"/>
internal sealed class SearchFieldsPager : ISearchFieldsPager
{
    #region implementation

    private const int MinimumPageSize = 5;
    private const int MaximumPageSize = 30;
    private const int ReservedTerminalRows = 9;
    private readonly IAnsiConsole _console;

    /**************************************************************/
    /// <summary>Defines navigation actions available below one field page.</summary>
    private enum PageChoice
    {
        /**************************************************************/
        /// <summary>Displays the following bounded page.</summary>
        Next,

        /**************************************************************/
        /// <summary>Displays the preceding bounded page.</summary>
        Previous,

        /**************************************************************/
        /// <summary>Returns to the iSearch dataset menu.</summary>
        Back
    }

    /**************************************************************/
    /// <summary>Initializes the fields pager with the destination console.</summary>
    /// <param name="console">The console receiving the field table and navigation prompts.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="console"/> is null.</exception>
    public SearchFieldsPager(IAnsiConsole console)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(console);
        _console = console;

        #endregion
    }

    /**************************************************************/
    /// <summary>Displays field metadata with bounded forward, backward, and escape navigation.</summary>
    /// <param name="fields">The validated field definitions to display.</param>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task representing field navigation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fields"/> is null.</exception>
    public async Task ShowAsync(IReadOnlyList<SearchField> fields, CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(fields);

        // Sort a copy so presentation order never mutates the API result retained by the flow.
        var orderedFields = fields
            .OrderBy(field => field.Name, StringComparer.Ordinal)
            .ToArray();
        var pageSize = Math.Clamp(_console.Profile.Height - ReservedTerminalRows, MinimumPageSize, MaximumPageSize);
        var pageCount = Math.Max(1, (orderedFields.Length + pageSize - 1) / pageSize);
        var pageIndex = 0;

        while (true)
        {
            if (orderedFields.Length == 0)
            {
                _console.WriteLine("iSearch fields: no fields were returned for this dataset.");
            }
            else
            {
                _console.Write(createTable(orderedFields.Skip(pageIndex * pageSize).Take(pageSize)));
            }

            var choices = new List<PageChoice>();
            if (pageIndex + 1 < pageCount)
            {
                choices.Add(PageChoice.Next);
            }

            if (pageIndex > 0)
            {
                choices.Add(PageChoice.Previous);
            }

            choices.Add(PageChoice.Back);
            var selected = await new SelectionPrompt<PageChoice>()
                .Title($"[bold orange1]iSearch Fields[/] — Page {pageIndex + 1} of {pageCount}")
                .HighlightStyle(new Style(Color.Black, Color.Orange1))
                .UseConverter(choice => choice switch
                {
                    PageChoice.Next => "Next Page",
                    PageChoice.Previous => "Previous Page",
                    PageChoice.Back => "Back to iSearch",
                    _ => choice.ToString()
                })
                .AddChoices(choices)
                .AddCancelResult(PageChoice.Back)
                .ShowAsync(_console, cancellationToken)
                .ConfigureAwait(false);

            switch (selected)
            {
                case PageChoice.Next:
                    pageIndex++;
                    break;
                case PageChoice.Previous:
                    pageIndex--;
                    break;
                case PageChoice.Back:
                    return;
                default:
                    throw new InvalidOperationException($"Unsupported iSearch fields action: {selected}");
            }
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Builds one escaped table for the selected page of field definitions.</summary>
    /// <param name="fields">The field definitions assigned to the current page.</param>
    /// <returns>A table with the seven documented field columns.</returns>
    private static Table createTable(IEnumerable<SearchField> fields)
    {
        #region implementation

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn("name")
            .AddColumn("displayName")
            .AddColumn("fieldType")
            .AddColumn("defaultQueryField")
            .AddColumn("defaultResultField")
            .AddColumn("multiValued")
            .AddColumn("searchOnly");

        foreach (var field in fields)
        {
            table.AddRow(
                Markup.Escape(field.Name),
                Markup.Escape(field.DisplayName ?? string.Empty),
                Markup.Escape(field.FieldType ?? string.Empty),
                formatBoolean(field.DefaultQueryField),
                formatBoolean(field.DefaultResultField),
                formatBoolean(field.MultiValued),
                formatBoolean(field.SearchOnly));
        }

        return table;

        #endregion
    }

    /**************************************************************/
    /// <summary>Formats an optional flag using the Boolean text expected by the operator view.</summary>
    /// <param name="value">The optional service flag.</param>
    /// <returns><c>True</c>, <c>False</c>, or an empty value when the flag was omitted.</returns>
    private static string formatBoolean(bool? value)
    {
        #region implementation

        return value.HasValue
            ? Markup.Escape(value.Value.ToString())
            : string.Empty;

        #endregion
    }

    #endregion
}
