using System.Text.Json;
using Carrot.Cli.ISearch.Contracts;
using Spectre.Console;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>Renders iSearch counts and generic JSON records in terminal-sized pages.</summary>
/// <remarks>
/// Records are serialized without terminal markup interpretation and remain in memory only for
/// the current workflow visit. The page size is bounded by the active terminal height.
/// </remarks>
/// <seealso cref="SearchResponse"/>
/// <seealso cref="ISearchResultsPager"/>
internal sealed class SearchResultsPager : ISearchResultsPager
{
    #region implementation

    private const int MinimumPageSize = 5;
    private const int MaximumPageSize = 30;
    private const int ReservedTerminalRows = 9;
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
    /// <summary>Displays the response records with bounded forward/back navigation.</summary>
    /// <param name="response">The validated response to render.</param>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task representing results navigation.</returns>
    public async Task ShowAsync(SearchResponse response, CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(response);

        // Reserve prompt rows so records do not scroll the navigation controls off the terminal.
        var pageSize = Math.Clamp(_console.Profile.Height - ReservedTerminalRows, MinimumPageSize, MaximumPageSize);
        var lines = new List<string>();

        // Serialize each record independently so page navigation can operate on display lines without changing JSON values.
        foreach (var result in response.Results)
        {
            lines.AddRange(JsonSerializer.Serialize(result, SerializerOptions).ReplaceLineEndings("\n").Split('\n'));
            lines.Add(string.Empty);
        }

        var pageCount = Math.Max(1, (lines.Count + pageSize - 1) / pageSize);
        var pageIndex = 0;
        while (true)
        {
            _console.WriteLine($"iSearch results: returned {response.ReturnedCount} of {response.TotalCount} total.");
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
                .Title($"[bold orange1]iSearch Results[/] — Page {pageIndex + 1} of {pageCount}")
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
                    throw new InvalidOperationException($"Unsupported iSearch results action: {selected}");
            }
        }

        #endregion
    }

    #endregion
}
