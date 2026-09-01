using Carrot.Cli.CarrotApi.Contracts;
using Spectre.Console;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>
/// Displays large Server Information responses in terminal-sized, navigable pages.
/// </summary>
/// <remarks>
/// The pager receives only presentation lines created by <see cref="ConsoleReporter"/> and
/// never changes the server response. Forward navigation is the default while another page exists;
/// Back and Escape return to the Server Information submenu.
/// </remarks>
/// <seealso cref="ConsoleReporter"/>
/// <seealso cref="ListResponse"/>
internal sealed class ServerInformationPager
{
    #region implementation

    private const int ReservedTerminalRows = 7;
    private readonly IAnsiConsole _console;
    private readonly ConsoleReporter _reporter;

    /**************************************************************/
    /// <summary>Defines navigation actions available below each server-information page.</summary>
    private enum PageChoice
    {
        /**************************************************************/
        /// <summary>Displays the preceding page.</summary>
        Previous,

        /**************************************************************/
        /// <summary>Displays the following page.</summary>
        Next,

        /**************************************************************/
        /// <summary>Returns to the Server Information submenu.</summary>
        Back
    }

    /**************************************************************/
    /// <summary>Initializes the pager with the interactive console and shared reporter.</summary>
    /// <param name="console">The console used for navigation.</param>
    /// <param name="reporter">The literal-text server-information reporter.</param>
    public ServerInformationPager(IAnsiConsole console, ConsoleReporter reporter)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(reporter);
        _console = console;
        _reporter = reporter;

        #endregion
    }

    /**************************************************************/
    /// <summary>Displays a response directly or as terminal-sized pages.</summary>
    /// <param name="configuration">The exact configuration response contract.</param>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task representing server-information page navigation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configuration"/> is null.</exception>
    /// <seealso cref="ConsoleReporter.CreateServerInfoLines"/>
    internal async Task ShowAsync(ListResponse configuration, CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(configuration);
        var lines = ConsoleReporter.CreateServerInfoLines(configuration);
        var pageSize = Math.Max(1, _console.Profile.Height - ReservedTerminalRows);
        var pageCount = Math.Max(1, (lines.Count + pageSize - 1) / pageSize);
        if (pageCount == 1)
        {
            _reporter.WriteServerInfoLines(lines);
            return;
        }

        var pageIndex = 0;
        while (true)
        {
            var pageLines = lines.Skip(pageIndex * pageSize).Take(pageSize).ToArray();
            _reporter.WriteServerInfoLines(pageLines);
            _console.MarkupLine($"[grey]Page {pageIndex + 1}/{pageCount}[/]");
            _console.MarkupLine("[grey]Press Escape to return to Server Information.[/]");

            var choices = new List<PageChoice>();
            if (pageIndex + 1 < pageCount)
            {
                // Spectre selects the first entry by default, so forward navigation comes first.
                choices.Add(PageChoice.Next);
            }

            if (pageIndex > 0)
            {
                choices.Add(PageChoice.Previous);
            }

            choices.Add(PageChoice.Back);
            var selected = await new SelectionPrompt<PageChoice>()
                .Title($"[bold orange1]Server Information[/] - Page {pageIndex + 1} of {pageCount}")
                .HighlightStyle(new Style(Color.Black, Color.Orange1))
                .UseConverter(choice => choice switch
                {
                    PageChoice.Previous => "Previous Page",
                    PageChoice.Next => "Next Page",
                    PageChoice.Back => "Back to Server Information",
                    _ => choice.ToString()
                })
                .AddChoices(choices)
                .AddCancelResult(PageChoice.Back)
                .ShowAsync(_console, cancellationToken)
                .ConfigureAwait(false);

            switch (selected)
            {
                case PageChoice.Previous:
                    pageIndex--;
                    break;
                case PageChoice.Next:
                    pageIndex++;
                    break;
                case PageChoice.Back:
                    return;
                default:
                    throw new InvalidOperationException($"Unsupported server-information page action: {selected}");
            }
        }

        #endregion
    }

    #endregion
}
