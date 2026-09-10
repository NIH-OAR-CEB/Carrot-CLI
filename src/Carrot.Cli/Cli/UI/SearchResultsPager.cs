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
/// Records are serialized without terminal markup interpretation. The current session retains every
/// fetched service page for an explicit Excel export, while terminal display pages remain presentation-only.
/// The pager owns prompt composition and terminal refreshes, but delegates cursor state, accumulation,
/// validation, throttling, and all-pages sequencing to <see cref="SearchResultPageSession"/>. This keeps
/// a long-running fetch from being confused with merely moving through already-rendered lines.
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
    private readonly ISearchResultsExportFlow? _exportFlow;

    /**************************************************************/
    /// <summary>Defines the independent terminal and service-data actions below one results view.</summary>
    /// <remarks>
    /// Display actions change only the current response's rendered lines. Fetch actions change the
    /// session's retained service data, and Save consumes that retained data without fetching more.
    /// </remarks>
    private enum PageChoice
    {
        /**************************************************************/
        /// <summary>Displays the following terminal page from the current service response.</summary>
        /// <remarks>No service request is made because all lines were already materialized.</remarks>
        NextDisplayPage,

        /**************************************************************/
        /// <summary>Displays the preceding terminal page from the current service response.</summary>
        /// <remarks>No retained records or service cursor state is changed.</remarks>
        PreviousDisplayPage,

        /**************************************************************/
        /// <summary>Fetches the following service result page using its continuation cursor.</summary>
        /// <remarks>Only one validated continuation response is adopted.</remarks>
        FetchNextResultPage,

        /**************************************************************/
        /// <summary>Fetches every remaining service result page using sequential cursor continuation.</summary>
        /// <remarks>The live Search Summary reports committed record progress while the session walks.</remarks>
        FetchAllPages,

        /**************************************************************/
        /// <summary>Saves every service result page walked in the current session to Excel.</summary>
        /// <remarks>The action does not issue a continuation request or alter the current page.</remarks>
        SaveResults,

        /**************************************************************/
        /// <summary>Returns to the iSearch dataset menu.</summary>
        /// <remarks>The session remains owned by the caller and is not persisted by this navigation action.</remarks>
        Back
    }

    /**************************************************************/
    /// <summary>Initializes the results pager with the destination console.</summary>
    /// <param name="console">The console receiving literal counts, records, and prompts.</param>
    /// <param name="footerRenderer">The shared renderer for result-navigation status.</param>
    /// <param name="exportFlow">The optional explicit iSearch Excel export flow.</param>
    /// <remarks>
    /// The export flow is optional so the pager can be used in focused result-navigation contexts. When
    /// supplied, Save receives the same session that owns the accepted pages and records.
    /// </remarks>
    /// <seealso cref="ApplicationFooterRenderer"/>
    /// <seealso cref="ISearchResultsExportFlow"/>
    public SearchResultsPager(
        IAnsiConsole console,
        ApplicationFooterRenderer footerRenderer,
        ISearchResultsExportFlow? exportFlow = null)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(footerRenderer);
        _console = console;
        _footerRenderer = footerRenderer;
        _exportFlow = exportFlow;

        #endregion
    }

    /**************************************************************/
    /// <summary>Displays cardinality, records, and two distinct layers of result navigation.</summary>
    /// <param name="session">The continuation-aware session for the current iSearch query.</param>
    /// <param name="cardinalityFieldNames">The configured labels for the common cardinality values.</param>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task representing results navigation.</returns>
    /// <remarks>
    /// The method remains in the prompt loop until the operator chooses Back or the prompt is canceled.
    /// Terminal display paging never calls iSearch. The cardinality panel, records, and Search Summary
    /// are one live target so every navigation and fetch update occurs in the original result region.
    /// </remarks>
    /// <exception cref="OperationCanceledException">Propagated when the supplied cancellation token is canceled.</exception>
    /// <seealso cref="SearchResultPageSession.FetchAllAsync"/>
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

        while (true)
        {
            // Start the live target at the same location where the original static result view would
            // have been written. The menu is part of the target, so the result summary and its controls
            // remain visible together without asking a SelectionPrompt to compete for the live console.
            _console.WriteLine();
            var initialChoices = createPageChoices(session, pageIndex, pageCount);
            var initialTarget = createInteractiveTarget(
                session,
                response,
                lines,
                pageIndex,
                pageCount,
                pageSize,
                cardinalityFieldNames,
                initialChoices,
                selectedIndex: 0,
                isFetchingAllPages: false);

            // Preserve the final result target when leaving normally, but switch to clearing before
            // Save so its separate TextPrompts can start at a clean cursor without stacking displays.
            var liveDisplay = _console.Live(initialTarget).AutoClear(false);
            var exitChoice = await liveDisplay
                .StartAsync<PageChoice>(async context =>
                {
                    // Keep the result view and custom keyboard menu in one live display. This avoids the
                    // terminal ownership conflict that occurs when SelectionPrompt runs inside LiveDisplay.
                    while (true)
                    {
                        var choices = createPageChoices(session, pageIndex, pageCount);
                        var selected = await readPageChoiceAsync(
                            context,
                            session,
                            response,
                            lines,
                            pageIndex,
                            pageCount,
                            pageSize,
                            cardinalityFieldNames,
                            choices,
                            cancellationToken).ConfigureAwait(false);

                        switch (selected)
                        {
                            case PageChoice.NextDisplayPage:
                                // The choices list guarantees a next display page exists, so moving forward
                                // cannot address a page beyond the materialized line range.
                                pageIndex++;
                                context.UpdateTarget(createInteractiveTarget(
                                    session,
                                    response,
                                    lines,
                                    pageIndex,
                                    pageCount,
                                    pageSize,
                                    cardinalityFieldNames,
                                    createPageChoices(session, pageIndex, pageCount),
                                    selectedIndex: 0,
                                    isFetchingAllPages: false));
                                break;

                            case PageChoice.PreviousDisplayPage:
                                // The choices list guarantees pageIndex is positive before moving backward.
                                pageIndex--;
                                context.UpdateTarget(createInteractiveTarget(
                                    session,
                                    response,
                                    lines,
                                    pageIndex,
                                    pageCount,
                                    pageSize,
                                    cardinalityFieldNames,
                                    createPageChoices(session, pageIndex, pageCount),
                                    selectedIndex: 0,
                                    isFetchingAllPages: false));
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
                                context.UpdateTarget(createInteractiveTarget(
                                    session,
                                    response,
                                    lines,
                                    pageIndex,
                                    pageCount,
                                    pageSize,
                                    cardinalityFieldNames,
                                    createPageChoices(session, pageIndex, pageCount),
                                    selectedIndex: 0,
                                    isFetchingAllPages: false));
                                break;

                            case PageChoice.FetchAllPages:
                                // The session owns the sequential loop; the pager updates the already
                                // running result target after each accepted response.
                                var allPagesResult = await fetchAllPagesAsync(
                                    session,
                                    context,
                                    cardinalityFieldNames,
                                    pageSize,
                                    choices,
                                    choices.IndexOf(PageChoice.FetchAllPages),
                                    cancellationToken).ConfigureAwait(false);

                                // A partial walk is still reflected by session.CurrentPage and progress; reset
                                // the target to its ordinary color and prompt guidance before the next choice.
                                response = session.CurrentPage;
                                lines = new List<string>();
                                populateLines(lines, response);
                                pageCount = calculatePageCount(lines.Count, pageSize);
                                pageIndex = 0;
                                context.UpdateTarget(createInteractiveTarget(
                                    session,
                                    response,
                                    lines,
                                    pageIndex,
                                    pageCount,
                                    pageSize,
                                    cardinalityFieldNames,
                                    createPageChoices(session, pageIndex, pageCount),
                                    selectedIndex: 0,
                                    isFetchingAllPages: false));

                                // The live target remains authoritative while diagnostics are written below it,
                                // preserving the original summary location for a retry or a normal exit.
                                if (allPagesResult.Status == OperationStatus.Failure)
                                {
                                    writeMessages(allPagesResult.Messages);
                                }

                                break;

                            case PageChoice.SaveResults:
                                // Leave LiveDisplay before export prompts acquire the console's exclusivity
                                // lock; the outer loop restores the results target after export completes.
                                liveDisplay.AutoClear = true;
                                return PageChoice.SaveResults;

                            case PageChoice.Back:
                                // Clear the complete transient results target before the parent workflow redraws
                                // the dataset menu; otherwise the old results actions remain visually adjacent
                                // to the new context.
                                liveDisplay.AutoClear = true;
                                return PageChoice.Back;

                            default:
                                // An unrecognized action would violate the prompt/dispatcher contract and
                                // should fail loudly during development rather than silently doing nothing.
                                throw new InvalidOperationException($"Unsupported iSearch results action: {selected}");
                        }
                    }
                })
                .ConfigureAwait(false);

            if (exitChoice == PageChoice.SaveResults && _exportFlow is not null)
            {
                // Export is intentionally outside LiveDisplay because its path and overwrite questions
                // are TextPrompts, which cannot run while a dynamic display owns the console.
                await _exportFlow.RunAsync(session, cancellationToken).ConfigureAwait(false);
                continue;
            }

            return;
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates the available result-navigation actions for the current session state.</summary>
    /// <param name="session">The active iSearch result session.</param>
    /// <param name="pageIndex">The zero-based terminal display-page index.</param>
    /// <param name="pageCount">The terminal display-page count.</param>
    /// <returns>The ordered actions shown by the in-place keyboard menu.</returns>
    /// <remarks>
    /// The order matches the former selection prompt: local display navigation comes first, then
    /// service fetches, optional export, and finally Back. The list is rebuilt after every accepted
    /// action so stale fetch or display choices cannot remain visible.
    /// </remarks>
    private List<PageChoice> createPageChoices(
        SearchResultPageSession session,
        int pageIndex,
        int pageCount)
    {
        #region implementation

        var choices = new List<PageChoice>();

        // Display actions operate only on materialized lines and never imply another service page.
        if (pageIndex + 1 < pageCount)
        {
            choices.Add(PageChoice.NextDisplayPage);
        }

        // The previous display action appears only after moving away from the first display page.
        if (pageIndex > 0)
        {
            choices.Add(PageChoice.PreviousDisplayPage);
        }

        // A service cursor is required before exposing either network action; display-page count is
        // intentionally irrelevant because one service page may span several terminal pages.
        if (session.CanFetchNextPage)
        {
            choices.Add(PageChoice.FetchNextResultPage);
            choices.Add(PageChoice.FetchAllPages);
        }

        // Save is supplied by composition and consumes retained data without issuing another request.
        if (_exportFlow is not null)
        {
            choices.Add(PageChoice.SaveResults);
        }

        choices.Add(PageChoice.Back);
        return choices;

        #endregion
    }

    /**************************************************************/
    /// <summary>Reads one result-navigation action while keeping the menu inside the live target.</summary>
    /// <param name="context">The live-display context that owns the result target.</param>
    /// <param name="session">The active iSearch result session.</param>
    /// <param name="response">The service response represented by the result region.</param>
    /// <param name="lines">The serialized terminal lines for the response.</param>
    /// <param name="pageIndex">The zero-based terminal display-page index.</param>
    /// <param name="pageCount">The terminal display-page count.</param>
    /// <param name="pageSize">The bounded number of serialized lines shown on one display page.</param>
    /// <param name="cardinalityFieldNames">The configured labels for cardinality values.</param>
    /// <param name="choices">The ordered actions available to the operator.</param>
    /// <param name="cancellationToken">The token that cancels keyboard input.</param>
    /// <returns>The selected action, or Back when Escape is pressed.</returns>
    /// <remarks>
    /// SelectionPrompt cannot safely run while LiveDisplay owns the same terminal. This focused input
    /// loop keeps equivalent Up, Down, Enter, and Escape behavior in the live renderable itself, making
    /// the menu visible while the single result target is refreshed.
    /// </remarks>
    /// <exception cref="OperationCanceledException">Propagated when keyboard input is canceled.</exception>
    /// <seealso cref="createInteractiveTarget"/>
    private async Task<PageChoice> readPageChoiceAsync(
        LiveDisplayContext context,
        SearchResultPageSession session,
        SearchResponse response,
        IReadOnlyList<string> lines,
        int pageIndex,
        int pageCount,
        int pageSize,
        SearchCardinalityFieldNames cardinalityFieldNames,
        IReadOnlyList<PageChoice> choices,
        CancellationToken cancellationToken)
    {
        #region implementation

        var selectedIndex = 0;
        while (true)
        {
            context.UpdateTarget(createInteractiveTarget(
                session,
                response,
                lines,
                pageIndex,
                pageCount,
                pageSize,
                cardinalityFieldNames,
                choices,
                selectedIndex,
                isFetchingAllPages: false));

            var key = await _console.Input.ReadKeyAsync(
                intercept: true,
                cancellationToken).ConfigureAwait(false);

            // A closed or unavailable input stream is equivalent to cancelling the result prompt;
            // leaving through Back avoids spinning while the live target remains active.
            if (!key.HasValue)
            {
                return PageChoice.Back;
            }

            switch (key.Value.Key)
            {
                case ConsoleKey.DownArrow:
                    // Do not wrap at the bottom; staying on the last action matches SelectionPrompt behavior.
                    if (selectedIndex + 1 < choices.Count)
                    {
                        selectedIndex++;
                    }

                    break;

                case ConsoleKey.UpArrow:
                    // Do not wrap at the top; the first action remains the initial selection.
                    if (selectedIndex > 0)
                    {
                        selectedIndex--;
                    }

                    break;

                case ConsoleKey.Enter:
                    // Enter commits only the currently highlighted action after the target has shown it.
                    return choices[selectedIndex];

                case ConsoleKey.Escape:
                    // Escape is the prompt's cancel path and returns to the owning iSearch menu.
                    return PageChoice.Back;
            }
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates the result region and its visible keyboard menu as one live target.</summary>
    /// <param name="session">The active iSearch result session.</param>
    /// <param name="response">The service response represented by the result region.</param>
    /// <param name="lines">The serialized terminal lines for the response.</param>
    /// <param name="pageIndex">The zero-based terminal display-page index.</param>
    /// <param name="pageCount">The terminal display-page count.</param>
    /// <param name="pageSize">The bounded number of serialized lines shown on one display page.</param>
    /// <param name="cardinalityFieldNames">The configured labels for cardinality values.</param>
    /// <param name="choices">The ordered actions shown below the Search Summary.</param>
    /// <param name="selectedIndex">The zero-based highlighted action.</param>
    /// <param name="isFetchingAllPages">Whether the region is showing an active all-pages walk.</param>
    /// <returns>A single live target containing data, summary, and controls.</returns>
    /// <remarks>
    /// Including the menu in the target prevents a second renderer from competing with LiveDisplay.
    /// During a walk the selected action remains visible while the summary's changing values refresh
    /// above it; keyboard input is intentionally paused until the walk completes.
    /// </remarks>
    /// <seealso cref="createResultView"/>
    private Rows createInteractiveTarget(
        SearchResultPageSession session,
        SearchResponse response,
        IReadOnlyList<string> lines,
        int pageIndex,
        int pageCount,
        int pageSize,
        SearchCardinalityFieldNames cardinalityFieldNames,
        IReadOnlyList<PageChoice> choices,
        int selectedIndex,
        bool isFetchingAllPages)
    {
        #region implementation

        var menuRows = new List<string>
        {
            $"[bold orange1]iSearch Results[/] - Result Page {response.Cardinality.PageNumber} of {response.Cardinality.TotalPages}; Display Page {pageIndex + 1} of {pageCount}"
        };

        if (isFetchingAllPages)
        {
            menuRows.Add("[orange1]Loading all remaining pages...[/]");
        }

        for (var index = 0; index < choices.Count; index++)
        {
            var label = getChoiceLabel(choices[index]);
            menuRows.Add(index == selectedIndex
                ? $"[black on orange1]> {Markup.Escape(label)}[/]"
                : $"  {Markup.Escape(label)}");
        }

        return new Rows(
            createResultView(
                session,
                response,
                lines,
                pageIndex,
                pageCount,
                pageSize,
                cardinalityFieldNames,
                isFetchingAllPages),
            new Markup(string.Join(Environment.NewLine, menuRows)));

        #endregion
    }

    /**************************************************************/
    /// <summary>Translates an internal result action into its operator-facing menu label.</summary>
    /// <param name="choice">The internal action value.</param>
    /// <returns>The stable label displayed in the result-navigation menu.</returns>
    private static string getChoiceLabel(PageChoice choice)
    {
        #region implementation

        return choice switch
        {
            PageChoice.NextDisplayPage => "Next Display Page",
            PageChoice.PreviousDisplayPage => "Previous Display Page",
            PageChoice.FetchNextResultPage => "Fetch Next Result Page",
            PageChoice.FetchAllPages => "Fetch All Pages",
            PageChoice.SaveResults => "Save iSearch Results to Excel",
            PageChoice.Back => "Back to iSearch",
            _ => choice.ToString()
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates the single live renderable region for cardinality, records, and the footer.</summary>
    /// <param name="session">The active iSearch result session.</param>
    /// <param name="response">The service response represented by the result region.</param>
    /// <param name="lines">The serialized terminal lines for the response.</param>
    /// <param name="pageIndex">The zero-based terminal display-page index.</param>
    /// <param name="pageCount">The terminal display-page count.</param>
    /// <param name="pageSize">The bounded number of serialized lines shown on one display page.</param>
    /// <param name="cardinalityFieldNames">The configured labels for cardinality values.</param>
    /// <param name="isFetchingAllPages">Whether the region is showing an active all-pages walk.</param>
    /// <returns>A single renderable whose target can be replaced in place by Spectre.Console.</returns>
    /// <remarks>
    /// Keeping the result content and footer in one target prevents a later live refresh from being
    /// positioned below a previously written static footer. The keyboard menu is added by
    /// <see cref="createInteractiveTarget"/> so it remains visible without competing for the terminal.
    /// </remarks>
    /// <seealso cref="ApplicationFooterRenderer.Create(ApplicationFooterState)"/>
    private Rows createResultView(
        SearchResultPageSession session,
        SearchResponse response,
        IReadOnlyList<string> lines,
        int pageIndex,
        int pageCount,
        int pageSize,
        SearchCardinalityFieldNames cardinalityFieldNames,
        bool isFetchingAllPages)
    {
        #region implementation

        var displayLines = lines
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToArray();
        var recordContent = response.Results.Count == 0
            ? new Text("No records matched the query.")
            : new Text(string.Join(Environment.NewLine, displayLines));

        return new Rows(
            createCardinalitySummary(response.Cardinality, cardinalityFieldNames),
            new Text(string.Empty),
            recordContent,
            new Text(string.Empty),
            _footerRenderer.Create(createFooterState(
                session,
                response,
                pageIndex,
                pageCount,
                isFetchingAllPages)));

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
    /// <remarks>Keeping one prompt page for empty data lets the operator leave the results view normally.</remarks>
    private static int calculatePageCount(int lineCount, int pageSize)
    {
        #region implementation

        // Empty results still need a prompt page so the operator can leave the view or see that no
        // service continuation is available.
        return Math.Max(1, (lineCount + pageSize - 1) / pageSize);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates the footer state for the current service response and retained walk.</summary>
    /// <param name="session">The active iSearch result session.</param>
    /// <param name="response">The response represented by the display lines.</param>
    /// <param name="pageIndex">The zero-based terminal display-page index.</param>
    /// <param name="pageCount">The terminal display-page count.</param>
    /// <param name="isFetchingAllPages">Whether the summary is being used by the live all-pages walk.</param>
    /// <returns>The state consumed by the shared footer renderer.</returns>
    /// <remarks>
    /// The current response supplies Data Page and current-chunk values, while the session progress
    /// snapshot supplies loaded records and the record-based percentage. This deliberate split keeps
    /// display-page navigation from changing the all-pages progress calculation.
    /// </remarks>
    /// <seealso cref="ApplicationFooterState"/>
    private static ApplicationFooterState createFooterState(
        SearchResultPageSession session,
        SearchResponse response,
        int pageIndex,
        int pageCount,
        bool isFetchingAllPages)
    {
        #region implementation

        var progress = session.WalkProgress;
        return new ApplicationFooterState
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
            LoadedResults = progress.LoadedRecords,
            LoadPercentage = progress.Percentage,
            CanFetchNextResultPage = session.CanFetchNextPage,
            IsFetchingAllPages = isFetchingAllPages,
            Instruction = isFetchingAllPages
                ? "Fetching all remaining result pages; the progress bar updates after each accepted data page."
                : "Next Display Page changes terminal lines; Fetch Next Result Page fetches one chunk; Fetch All Pages walks to the end."
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>Runs the session's all-pages walk while refreshing the existing result target.</summary>
    /// <param name="session">The active iSearch result session.</param>
    /// <param name="context">The live-display context that owns the original result target.</param>
    /// <param name="cardinalityFieldNames">The configured labels for cardinality values.</param>
    /// <param name="pageSize">The bounded terminal line capacity for each display page.</param>
    /// <param name="choices">The actions currently displayed in the keyboard menu.</param>
    /// <param name="selectedIndex">The highlighted action while the walk is active.</param>
    /// <param name="cancellationToken">The token that cancels the walk.</param>
    /// <returns>The completed final page or an expected walk failure.</returns>
    /// <remarks>
    /// The session callback runs only after a continuation page is committed. Each callback rebuilds
    /// the complete result target at its original location, including orange paging/progress values
    /// while the walk is active. The caller resets the target to ordinary styling after the walk.
    /// </remarks>
    /// <exception cref="OperationCanceledException">Propagated when the walk cancellation token is canceled.</exception>
    /// <seealso cref="SearchResultPageSession.FetchAllAsync"/>
    /// <seealso cref="createResultView"/>
    private async Task<OperationResult<SearchResponse>> fetchAllPagesAsync(
        SearchResultPageSession session,
        LiveDisplayContext context,
        SearchCardinalityFieldNames cardinalityFieldNames,
        int pageSize,
        IReadOnlyList<PageChoice> choices,
        int selectedIndex,
        CancellationToken cancellationToken)
    {
        #region implementation

        return await session.FetchAllAsync(
            progress =>
            {
                var response = session.CurrentPage;
                var lines = new List<string>();
                populateLines(lines, response);
                var pageCount = calculatePageCount(lines.Count, pageSize);

                // The progress callback is raised after adoption, so the target can show the newly
                // committed Data Page and loaded-record percentage without exposing speculative state.
                context.UpdateTarget(createInteractiveTarget(
                    session,
                    response,
                    lines,
                    0,
                    pageCount,
                    pageSize,
                    cardinalityFieldNames,
                    choices,
                    selectedIndex,
                    isFetchingAllPages: true));
            },
            cancellationToken).ConfigureAwait(false);

        #endregion
    }

    /**************************************************************/
    /// <summary>Builds the colored summary that keeps service cardinality visible above navigation.</summary>
    /// <param name="cardinality">The typed counts and result-page values reported by iSearch.</param>
    /// <param name="fieldNames">The configured labels for the cardinality values.</param>
    /// <returns>A bounded panel containing literal cardinality text.</returns>
    /// <remarks>The values remain plain text so service metadata cannot be interpreted as terminal markup.</remarks>
    /// <seealso cref="SearchCardinality"/>
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
    /// <remarks>
    /// Messages are emitted below the active result target; the persistent live display continues to
    /// own and refresh the cardinality, records, and Search Summary region above them.
    /// </remarks>
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
