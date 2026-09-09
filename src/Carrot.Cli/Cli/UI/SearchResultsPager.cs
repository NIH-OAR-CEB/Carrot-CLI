using System.Text.Json;
using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Carrot.Cli.ISearch;
using Carrot.Cli.ISearch.Contracts;
using Spectre.Console;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>Renders iSearch cardinality and generic JSON records with separate display and data navigation.</summary>
/// <remarks>
/// Records are serialized without terminal markup interpretation and remain in memory only for
/// the current workflow visit. Terminal display pages never represent service result pages.
/// </remarks>
/// <seealso cref="SearchResultPageSession"/>
/// <seealso cref="SearchResponse"/>
/// <seealso cref="ISearchResultsPager"/>
internal sealed class SearchResultsPager : ISearchResultsPager
{
    #region implementation

    private const int MinimumPageSize = 5;
    private const int MaximumPageSize = 30;
    private const int ReservedTerminalRows = 16;
    private const int CardinalitySummaryRows = 3;
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private readonly IAnsiConsole _console;
    private readonly ApplicationFooterRenderer _footerRenderer;

    /**************************************************************/
    /// <summary>Defines the independent terminal and service-data actions below one results view.</summary>
    private enum PageChoice
    {
        /**************************************************************/
        /// <summary>Displays the following terminal page from the current service response.</summary>
        NextDisplayPage,

        /**************************************************************/
        /// <summary>Displays the preceding terminal page from the current service response.</summary>
        PreviousDisplayPage,

        /**************************************************************/
        /// <summary>Fetches the following service result page using its continuation cursor.</summary>
        FetchNextResultPage,

        /**************************************************************/
        /// <summary>Returns to the iSearch dataset menu.</summary>
        Back
    }

    /**************************************************************/
    /// <summary>Initializes the results pager with the destination console.</summary>
    /// <param name="console">The console receiving literal counts, records, and prompts.</param>
    /// <param name="footerRenderer">The shared renderer for result-navigation status.</param>
    public SearchResultsPager(IAnsiConsole console, ApplicationFooterRenderer footerRenderer)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(footerRenderer);
        _console = console;
        _footerRenderer = footerRenderer;

        #endregion
    }

    /**************************************************************/
    /// <summary>Displays cardinality, records, and two distinct layers of result navigation.</summary>
    /// <param name="session">The continuation-aware session for the current iSearch query.</param>
    /// <param name="cardinalityFieldNames">The configured labels for the common cardinality values.</param>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task representing results navigation.</returns>
    public async Task ShowAsync(
        SearchResultPageSession session,
        SearchCardinalityFieldNames cardinalityFieldNames,
        CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(cardinalityFieldNames);

        var response = session.CurrentPage;

        // Reserve prompt rows and the cardinality summary so the active results view keeps its
        // navigation controls usable when the terminal is shorter than the normal page height.
        var pageSize = Math.Clamp(
            _console.Profile.Height - ReservedTerminalRows - CardinalitySummaryRows,
            MinimumPageSize,
            MaximumPageSize);
        var lines = new List<string>();
        populateLines(lines, response);
        var pageCount = calculatePageCount(lines.Count, pageSize);
        var pageIndex = 0;

        // Keep rendering and prompting in one loop so every navigation choice redraws the current
        // cardinality header and only a fetch action can replace the service response.
        while (true)
        {
            // Leave a visual break after preceding informational output, then render the summary
            // before records so service counts remain associated with the displayed data chunk.
            _console.WriteLine();
            _console.Write(createCardinalitySummary(response.Cardinality, cardinalityFieldNames));
            _console.WriteLine();

            // Zero results still produce the colored summary, but there are no record lines to slice.
            // The explicit message makes an empty successful response distinguishable from failure.
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

            _footerRenderer.Render(new ApplicationFooterState
            {
                Context = "iSearch Results",
                Dataset = session.Dataset,
                ReturnDataset = session.ReturnDataset,
                ResultPage = response.Cardinality.PageNumber,
                ResultPageCount = response.Cardinality.TotalPages,
                DisplayPage = pageIndex + 1,
                DisplayPageCount = pageCount,
                CurrentResults = response.Cardinality.CurrentResults,
                TotalResults = response.Cardinality.TotalResults,
                CanFetchNextResultPage = session.CanFetchNextPage,
                Instruction = "Next Display Page changes terminal lines; Fetch Next Result Page fetches one data chunk."
            });

            var choices = new List<PageChoice>();

            // Display actions operate only on the materialized lines and never imply another
            // service page is available.
            if (pageIndex + 1 < pageCount)
            {
                choices.Add(PageChoice.NextDisplayPage);
            }

            // The previous display action appears only after moving away from the first display page.
            if (pageIndex > 0)
            {
                choices.Add(PageChoice.PreviousDisplayPage);
            }

            // A service cursor and remaining cardinality pages are both required before exposing a
            // network action; terminal display-page count is intentionally irrelevant here.
            if (session.CanFetchNextPage)
            {
                choices.Add(PageChoice.FetchNextResultPage);
            }

            choices.Add(PageChoice.Back);
            var selected = await new SelectionPrompt<PageChoice>()
                .Title($"[bold orange1]iSearch Results[/] - Result Page {response.Cardinality.PageNumber} of {response.Cardinality.TotalPages}; Display Page {pageIndex + 1} of {pageCount}")
                .HighlightStyle(new Style(Color.Black, Color.Orange1))
                .UseConverter(choice => choice switch
                {
                    // Translate internal navigation values into labels that cannot be confused with
                    // the service result-page transition.
                    PageChoice.NextDisplayPage => "Next Display Page",
                    PageChoice.PreviousDisplayPage => "Previous Display Page",
                    PageChoice.FetchNextResultPage => "Fetch Next Result Page",
                    PageChoice.Back => "Back to iSearch",
                    _ => choice.ToString()
                })
                .AddChoices(choices)
                .AddCancelResult(PageChoice.Back)
                .ShowAsync(_console, cancellationToken)
                .ConfigureAwait(false);

            switch (selected)
            {
                case PageChoice.NextDisplayPage:
                    // The choices list guarantees a next display page exists, so moving forward
                    // cannot address a page beyond the materialized line range.
                    pageIndex++;
                    break;

                case PageChoice.PreviousDisplayPage:
                    // The choices list guarantees pageIndex is positive before moving backward.
                    pageIndex--;
                    break;

                case PageChoice.FetchNextResultPage:
                    var result = await session.FetchNextAsync(cancellationToken).ConfigureAwait(false);
                    if (result.Status == OperationStatus.Failure)
                    {
                        // Keep the validated current page visible after a failed fetch so the user
                        // can retry without losing the last data chunk or its continuation cursor.
                        writeMessages(result.Messages);
                        break;
                    }

                    response = session.CurrentPage;
                    lines = new List<string>();
                    populateLines(lines, response);
                    pageCount = calculatePageCount(lines.Count, pageSize);
                    pageIndex = 0;
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
    /// <summary>Serializes the current service response into safe terminal display lines.</summary>
    /// <param name="lines">The mutable display-line collection to populate.</param>
    /// <param name="response">The service response whose generic records are displayed.</param>
    private static void populateLines(List<string> lines, SearchResponse response)
    {
        #region implementation

        // Serialize each record independently so terminal navigation cannot change JSON values or
        // mix records from two service result pages.
        foreach (var result in response.Results)
        {
            lines.AddRange(JsonSerializer.Serialize(result, SerializerOptions).ReplaceLineEndings("\n").Split('\n'));
            lines.Add(string.Empty);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Calculates the minimum one-based display-page count for a response.</summary>
    /// <param name="lineCount">The number of serialized terminal lines.</param>
    /// <param name="pageSize">The bounded number of lines available per display page.</param>
    /// <returns>At least one display page, including for an empty response.</returns>
    private static int calculatePageCount(int lineCount, int pageSize)
    {
        #region implementation

        // Empty results still need a prompt page so the operator can leave the view or see that no
        // service continuation is available.
        return Math.Max(1, (lineCount + pageSize - 1) / pageSize);

        #endregion
    }

    /**************************************************************/
    /// <summary>Builds the colored summary that keeps service cardinality visible above navigation.</summary>
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

    /**************************************************************/
    /// <summary>Writes safe continuation failures as literal terminal lines.</summary>
    /// <param name="messages">The bounded operation messages returned by the API session.</param>
    private void writeMessages(IReadOnlyList<OperationMessage> messages)
    {
        #region implementation

        foreach (var message in messages)
        {
            // Service diagnostics must remain literal because they can contain arbitrary text.
            _console.WriteLine($"iSearch: {message.Message}");
        }

        #endregion
    }

    #endregion
}
