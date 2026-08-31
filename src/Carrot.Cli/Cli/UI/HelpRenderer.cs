using Spectre.Console;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>
/// Renders curated help topics for commands, formats, privacy, scheduling, and failures.
/// </summary>
internal sealed class HelpRenderer
{
    #region implementation

    private const int MinimumPageLineCount = 6;
    private const int ReservedTerminalRows = 9;
    private readonly IAnsiConsole _console;
    private readonly HelpTopicCatalog _catalog;
    private readonly IHelpContentProvider _contentProvider;
    private readonly MarkdownHelpRenderer _markdownRenderer;

    /**************************************************************/
    /// <summary>Defines stable navigation actions available below paged help content.</summary>
    private enum PageChoice
    {
        /**************************************************************/
        /// <summary>Displays the following help page.</summary>
        Next,

        /**************************************************************/
        /// <summary>Displays the preceding help page.</summary>
        Previous,

        /**************************************************************/
        /// <summary>Closes the current help topic.</summary>
        Close
    }

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
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task containing <see langword="true"/> when the requested topic was found and rendered.</returns>
    internal async Task<bool> RenderAsync(string? topic, CancellationToken cancellationToken)
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

        var pages = createPages(markdown);
        if (!_console.Profile.Capabilities.Interactive || pages.Count == 1)
        {
            _markdownRenderer.Render(markdown);
            return true;
        }

        await showPagesAsync(pages, cancellationToken).ConfigureAwait(false);
        return true;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Splits Markdown at safe block boundaries when its estimated rendered height exceeds the terminal.
    /// </summary>
    /// <param name="markdown">The complete trusted help document.</param>
    /// <returns>One or more independently renderable Markdown pages.</returns>
    private IReadOnlyList<string> createPages(string markdown)
    {
        #region implementation

        var blocks = splitIntoBlocks(markdown);
        var lineBudget = Math.Max(MinimumPageLineCount, _console.Profile.Height - ReservedTerminalRows);
        var contentWidth = Math.Max(20, _console.Profile.Width - 4);
        var pages = new List<string>();
        var pageBlocks = new List<string>();
        var pageLineCount = 0;

        foreach (var block in blocks)
        {
            var blockLineCount = estimateRenderedLineCount(block, contentWidth);
            if (pageBlocks.Count > 0 && pageLineCount + blockLineCount > lineBudget)
            {
                pages.Add(string.Join(Environment.NewLine + Environment.NewLine, pageBlocks));
                pageBlocks.Clear();
                pageLineCount = 0;
            }

            pageBlocks.Add(block);
            pageLineCount += blockLineCount;
        }

        if (pageBlocks.Count > 0)
        {
            pages.Add(string.Join(Environment.NewLine + Environment.NewLine, pageBlocks));
        }

        return pages.Count == 0 ? [markdown] : pages;

        #endregion
    }

    /**************************************************************/
    /// <summary>Displays help pages until Close Help or Escape returns to the calling menu or command.</summary>
    /// <param name="pages">The ordered independently renderable Markdown pages.</param>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task representing pager navigation.</returns>
    private async Task showPagesAsync(IReadOnlyList<string> pages, CancellationToken cancellationToken)
    {
        #region implementation

        var pageIndex = 0;
        while (true)
        {
            _markdownRenderer.Render(pages[pageIndex]);
            _console.MarkupLine($"[grey]Page {pageIndex + 1}/{pages.Count}[/]");
            _console.MarkupLine("[grey]Press Escape to close help.[/]");

            var choices = new List<PageChoice>();
            if (pageIndex + 1 < pages.Count)
            {
                // Spectre selects the first entry by default, so forward navigation comes first.
                choices.Add(PageChoice.Next);
            }
            else
            {
                // Closing is the default on the final page to avoid cycling backward accidentally.
                choices.Add(PageChoice.Close);
            }

            if (pageIndex > 0)
            {
                choices.Add(PageChoice.Previous);
            }

            if (!choices.Contains(PageChoice.Close))
            {
                choices.Add(PageChoice.Close);
            }

            var selected = await new SelectionPrompt<PageChoice>()
                .Title($"[bold orange1]Help[/] — Page {pageIndex + 1} of {pages.Count}")
                .HighlightStyle(new Style(Color.Black, Color.Orange1))
                .UseConverter(choice => choice switch
                {
                    PageChoice.Next => "Next Page",
                    PageChoice.Previous => "Previous Page",
                    PageChoice.Close => "Close Help",
                    _ => choice.ToString()
                })
                .AddChoices(choices)
                .AddCancelResult(PageChoice.Close)
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
                case PageChoice.Close:
                    return;
                default:
                    throw new InvalidOperationException($"Unsupported help action: {selected}");
            }
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Divides Markdown into paragraphs, headings, list items, and complete fenced-code blocks.</summary>
    /// <param name="markdown">The complete trusted help document.</param>
    /// <returns>Ordered blocks that can be rendered independently without changing Markdown meaning.</returns>
    private static IReadOnlyList<string> splitIntoBlocks(string markdown)
    {
        #region implementation

        var blocks = new List<string>();
        var paragraphLines = new List<string>();
        var fencedLines = new List<string>();
        var inCodeFence = false;

        using var reader = new StringReader(markdown.ReplaceLineEndings("\n"));
        while (reader.ReadLine() is { } line)
        {
            if (inCodeFence)
            {
                fencedLines.Add(line);
                if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    blocks.Add(string.Join(Environment.NewLine, fencedLines));
                    fencedLines.Clear();
                    inCodeFence = false;
                }

                continue;
            }

            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                flushParagraphBlock(blocks, paragraphLines);
                fencedLines.Add(line);
                inCodeFence = true;
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                flushParagraphBlock(blocks, paragraphLines);
                continue;
            }

            if (isStandaloneBlock(line))
            {
                flushParagraphBlock(blocks, paragraphLines);
                blocks.Add(line.Trim());
                continue;
            }

            paragraphLines.Add(line.Trim());
        }

        flushParagraphBlock(blocks, paragraphLines);
        if (fencedLines.Count > 0)
        {
            blocks.Add(string.Join(Environment.NewLine, fencedLines));
        }

        return blocks;

        #endregion
    }

    /**************************************************************/
    /// <summary>Appends one normalized paragraph block and clears its source-line buffer.</summary>
    /// <param name="blocks">The destination block collection.</param>
    /// <param name="paragraphLines">The buffered source lines belonging to one paragraph.</param>
    private static void flushParagraphBlock(ICollection<string> blocks, ICollection<string> paragraphLines)
    {
        #region implementation

        if (paragraphLines.Count == 0)
        {
            return;
        }

        blocks.Add(string.Join(' ', paragraphLines));
        paragraphLines.Clear();

        #endregion
    }

    /**************************************************************/
    /// <summary>Identifies headings and list items that the terminal renderer emits independently.</summary>
    /// <param name="line">The current Markdown source line.</param>
    /// <returns><see langword="true"/> when the line should be its own pagination block.</returns>
    private static bool isStandaloneBlock(string line)
    {
        #region implementation

        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("# ", StringComparison.Ordinal)
            || trimmed.StartsWith("## ", StringComparison.Ordinal)
            || trimmed.StartsWith("### ", StringComparison.Ordinal))
        {
            return true;
        }

        if (trimmed.Length >= 2
            && (trimmed[0] is '-' or '+' or '*')
            && char.IsWhiteSpace(trimmed[1]))
        {
            return true;
        }

        var ordinalEnd = 0;
        while (ordinalEnd < trimmed.Length && char.IsDigit(trimmed[ordinalEnd]))
        {
            ordinalEnd++;
        }

        return ordinalEnd > 0
            && ordinalEnd + 1 < trimmed.Length
            && trimmed[ordinalEnd] == '.'
            && char.IsWhiteSpace(trimmed[ordinalEnd + 1]);

        #endregion
    }

    /**************************************************************/
    /// <summary>Estimates terminal rows consumed by one rendered Markdown block.</summary>
    /// <param name="block">One independently renderable Markdown block.</param>
    /// <param name="contentWidth">The conservative terminal width available to rendered content.</param>
    /// <returns>The estimated positive rendered-line count.</returns>
    private static int estimateRenderedLineCount(string block, int contentWidth)
    {
        #region implementation

        if (block.TrimStart().StartsWith("```", StringComparison.Ordinal))
        {
            var lines = block.ReplaceLineEndings("\n").Split('\n');
            var contentLines = lines.Length > 1 && lines[^1].TrimStart().StartsWith("```", StringComparison.Ordinal)
                ? lines[1..^1]
                : lines[1..];
            return contentLines.Sum(line => estimateWrappedLineCount(line, contentWidth)) + 3;
        }

        var renderedLineCount = estimateWrappedLineCount(block, contentWidth);
        return isStandaloneBlock(block) ? renderedLineCount : renderedLineCount + 1;

        #endregion
    }

    /**************************************************************/
    /// <summary>Estimates width-based wrapping for one source line.</summary>
    /// <param name="line">The source line whose visible width is estimated.</param>
    /// <param name="contentWidth">The conservative terminal content width.</param>
    /// <returns>At least one terminal row.</returns>
    private static int estimateWrappedLineCount(string line, int contentWidth)
    {
        #region implementation

        return Math.Max(1, (line.Length + contentWidth - 1) / contentWidth);

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
