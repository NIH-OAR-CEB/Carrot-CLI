using Spectre.Console;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>
/// Displays the editable welcome and getting-started Markdown before the main menu.
/// </summary>
/// <seealso cref="IApplicationPreambleProvider"/>
/// <seealso cref="MarkdownHelpRenderer"/>
internal sealed class ApplicationPreambleRenderer
{
    #region implementation

    private readonly IAnsiConsole _console;
    private readonly IApplicationPreambleProvider _contentProvider;
    private readonly MarkdownHelpRenderer _markdownRenderer;

    /**************************************************************/
    /// <summary>
    /// Initializes the preamble renderer with file-content and console-presentation boundaries.
    /// </summary>
    /// <param name="console">The console receiving preamble or fallback output.</param>
    /// <param name="contentProvider">The provider that reads current external Markdown.</param>
    /// <param name="markdownRenderer">The shared safe Markdown renderer.</param>
    public ApplicationPreambleRenderer(
        IAnsiConsole console,
        IApplicationPreambleProvider contentProvider,
        MarkdownHelpRenderer markdownRenderer)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(contentProvider);
        ArgumentNullException.ThrowIfNull(markdownRenderer);

        _console = console;
        _contentProvider = contentProvider;
        _markdownRenderer = markdownRenderer;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Reads and renders the current preamble or displays nonfatal recovery guidance.
    /// </summary>
    internal void Render()
    {
        #region implementation

        var markdown = _contentProvider.Read();
        if (string.IsNullOrWhiteSpace(markdown))
        {
            _console.MarkupLine(
                "[yellow]Welcome information is unavailable.[/] Run [yellow]carrot-cli help getting-started[/] for guidance.");
            _console.WriteLine();
            return;
        }

        _markdownRenderer.Render(markdown);

        #endregion
    }

    #endregion
}
