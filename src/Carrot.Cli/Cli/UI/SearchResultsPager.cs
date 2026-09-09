using System.Text.Json;
using Carrot.Cli.Configuration;
using Carrot.Cli.ISearch.Contracts;
using Spectre.Console;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>Renders a colored cardinality summary and generic JSON records in terminal-sized pages.</summary>
/// <remarks>
/// Records are serialized without terminal markup interpretation and remain in memory only for
/// the current workflow visit. The page size is bounded by the active terminal height, leaving a
/// reserved summary region before the navigation prompt.
/// </remarks>
/// <seealso cref="SearchResponse"/>
/// <seealso cref="ISearchResultsPager"/>
internal sealed class SearchResultsPager : ISearchResultsPager
{
    #region implementation

    private const int MinimumPageSize = 5;
    private const int MaximumPageSize = 30;
    private const int ReservedTerminalRows = 9;
    private const int CardinalitySummaryRows = 3;
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private readonly IAnsiConsole _console;

    /**************************************************************/
    /// <summary>Defines navigation actions available below one results page.</summary>
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
    /// <summary>Initializes the results pager with the destination console.</summary>
    /// <param name="console">The console receiving literal counts, records, and prompts.</param>
    public SearchResultsPager(IAnsiConsole console)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(console);
        _console = console;

        #endregion
    }

    /**************************************************************/
    /// <summary>Displays a separated cardinality summary, response records, and bounded navigation.</summary>
    /// <param name="response">The validated response to render.</param>
    /// <param name="cardinalityFieldNames">The configured labels for the common cardinality values.</param>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task representing results navigation.</returns>
    public async Task ShowAsync(
        SearchResponse response,
        SearchCardinalityFieldNames cardinalityFieldNames,
        CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(cardinalityFieldNames);

        // Reserve prompt rows and the cardinality summary so the active results view keeps its
        // navigation controls usable when the terminal is shorter than the normal page height.
        var pageSize = Math.Clamp(
            _console.Profile.Height - ReservedTerminalRows - CardinalitySummaryRows,
            MinimumPageSize,
            MaximumPageSize);
        var lines = new List<string>();

        // Serialize each record independently so page navigation can operate on display lines without changing JSON values.
        foreach (var result in response.Results)
        {
            lines.AddRange(JsonSerializer.Serialize(result, SerializerOptions).ReplaceLineEndings("\n").Split('\n'));
            lines.Add(string.Empty);
        }

        var pageCount = Math.Max(1, (lines.Count + pageSize - 1) / pageSize);
        var pageIndex = 0;

        // Keep rendering and prompting in one loop so every navigation choice redraws the same
        // cardinality header and the corresponding bounded slice of records.
        while (true)
        {
            // Leave a visual break after the preceding informational output, then render the summary
            // before records so the counts remain immediately associated with the completed request.
            _console.WriteLine();
            _console.Write(createCardinalitySummary(response.Cardinality, cardinalityFieldNames));
            _console.WriteLine();

            // Zero results still produce the colored summary, but there are no record lines to slice.
            // The explicit message makes an empty successful walk distinguishable from a malformed response.
            if (response.Results.Count == 0)
            {
                _console.WriteLine("No records matched the query.");
            }
            else
            {
                foreach (var line in lines.Skip(pageIndex * pageSize).Take(pageSize))
                {
                    _console.WriteLine(line);
                }
            }

            var choices = new List<PageChoice>();

            // Only offer forward navigation while another display page exists. Keeping unavailable
            // actions out of the prompt prevents pageIndex from exceeding the rendered line range.
            if (pageIndex + 1 < pageCount)
            {
                choices.Add(PageChoice.Next);
            }

            // The previous action appears only after moving away from the first display page.
            if (pageIndex > 0)
            {
                choices.Add(PageChoice.Previous);
            }

            choices.Add(PageChoice.Back);
            var selected = await new SelectionPrompt<PageChoice>()
                .Title($"[bold orange1]iSearch Results[/] — Page {pageIndex + 1} of {pageCount}")
                .HighlightStyle(new Style(Color.Black, Color.Orange1))
                .UseConverter(choice => choice switch
                {
                    // Translate internal navigation values into labels that explain the action to a
                    // terminal user; the enum remains the value used by the dispatcher below.
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
                    // The choices list guarantees a next page exists, so moving forward cannot
                    // address a page beyond the materialized display lines.
                    pageIndex++;
                    break;

                case PageChoice.Previous:
                    // The choices list guarantees pageIndex is positive before moving backward.
                    pageIndex--;
                    break;

                case PageChoice.Back:
                    // Returning stops this pager and hands control back to the dataset menu.
                    return;

                default:
                    // An unrecognized action would violate the prompt/dispatcher contract and
                    // should fail loudly during development rather than silently doing nothing.
                    throw new InvalidOperationException($"Unsupported iSearch results action: {selected}");
            }
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Builds the colored summary that keeps result-walk cardinality visible above navigation.</summary>
    /// <param name="cardinality">The typed counts and result-page values reported by iSearch.</param>
    /// <param name="fieldNames">The configured labels for the cardinality values.</param>
    /// <returns>A bounded panel containing literal cardinality text.</returns>
    private static Panel createCardinalitySummary(
        SearchCardinality cardinality,
        SearchCardinalityFieldNames fieldNames)
    {
        #region implementation

        // Keep the configured labels and service values literal inside the panel so configuration or
        // response text cannot be interpreted as Spectre markup.
        var summary =
            $"{fieldNames.TotalResultsFieldName}: {cardinality.TotalResults} | "
            + $"{fieldNames.CurrentResultsFieldName}: {cardinality.CurrentResults} | "
            + $"{fieldNames.PageNumberFieldName}: {cardinality.PageNumber} of "
            + $"{fieldNames.TotalPagesFieldName}: {cardinality.TotalPages}";

        // A panel gives the footer a stable visual boundary and a distinct color without requiring
        // a live display that would compete with the existing interactive selection prompt.
        return new Panel(new Text(summary, new Style(Color.White)))
            .Header("[bold cyan1]Result Cardinality[/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(Color.Cyan1));

        #endregion
    }

    #endregion
}
