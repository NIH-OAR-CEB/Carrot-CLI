using Spectre.Console;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>
/// Renders curated help topics for commands, formats, privacy, scheduling, and failures.
/// </summary>
internal sealed class HelpRenderer
{
    #region implementation

    private readonly IAnsiConsole _console;
    private readonly HelpTopicCatalog _catalog;
    private readonly IHelpContentProvider _contentProvider;
    private readonly MarkdownHelpRenderer _markdownRenderer;

    /**************************************************************/
    /// <summary>
    /// Initializes the curated help renderer and its resource and presentation collaborators.
    /// </summary>
    /// <param name="console">The console used for errors and available-topic output.</param>
    /// <param name="catalog">The immutable topic and alias catalog.</param>
    /// <param name="contentProvider">The embedded Markdown resource provider.</param>
    /// <param name="markdownRenderer">The terminal Markdown renderer.</param>
    public HelpRenderer(
        IAnsiConsole console,
        HelpTopicCatalog catalog,
        IHelpContentProvider contentProvider,
        MarkdownHelpRenderer markdownRenderer)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(contentProvider);
        ArgumentNullException.ThrowIfNull(markdownRenderer);

        _console = console;
        _catalog = catalog;
        _contentProvider = contentProvider;
        _markdownRenderer = markdownRenderer;

        #endregion
    }

    /**************************************************************/
    /// <summary>Gets every available topic in interactive display order.</summary>
    internal IReadOnlyList<HelpTopic> Topics => _catalog.Topics;

    /**************************************************************/
    /// <summary>
    /// Renders the requested topic or the getting-started overview when no topic is supplied.
    /// </summary>
    /// <param name="topic">The optional topic name.</param>
    /// <returns><see langword="true"/> when the requested topic was found and rendered.</returns>
    internal bool Render(string? topic)
    {
        #region implementation

        if (!_catalog.TryResolve(topic, out var resolvedTopic))
        {
            _console.MarkupLine($"[red]Unknown help topic:[/] {Markup.Escape(topic ?? string.Empty)}");
            renderAvailableTopics();
            return false;
        }

        var markdown = _contentProvider.Read(resolvedTopic);
        if (markdown is null)
        {
            _console.MarkupLine(
                $"[red]Help content is unavailable for topic[/] [yellow]{Markup.Escape(resolvedTopic.Key)}[/].");
            return false;
        }

        _markdownRenderer.Render(markdown);
        return true;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Displays canonical topic keys after an invalid named-help request.
    /// </summary>
    private void renderAvailableTopics()
    {
        #region implementation

        _console.MarkupLine("[bold]Available topics:[/]");
        foreach (var availableTopic in _catalog.Topics)
        {
            _console.MarkupLine(
                $"  [orange1]•[/] [yellow]{Markup.Escape(availableTopic.Key)}[/] — {Markup.Escape(availableTopic.Title)}");
        }

        #endregion
    }

    #endregion
}
