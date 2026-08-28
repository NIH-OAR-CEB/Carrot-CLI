using System.Text;
using System.Text.RegularExpressions;
using Spectre.Console;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>
/// Renders the bounded Markdown subset used by embedded terminal help with Spectre styling.
/// </summary>
/// <remarks>
/// Supported constructs are ATX headings, paragraphs, ordered and unordered lists,
/// fenced code blocks, inline code, emphasis, and links. Unsupported constructs are
/// emitted as escaped paragraph text so help content cannot inject Spectre markup.
/// </remarks>
/// <seealso cref="HelpRenderer"/>
internal sealed class MarkdownHelpRenderer
{
    #region implementation

    private static readonly Regex UnorderedListPattern = new(
        @"^\s*[-+*]\s+(?<content>.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex OrderedListPattern = new(
        @"^\s*(?<number>\d+)\.\s+(?<content>.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex InlineTokenPattern = new(
        @"(`[^`\r\n]+`|\*\*[^*\r\n]+\*\*|(?<!\*)\*[^*\r\n]+\*(?!\*)|\[[^\]\r\n]+\]\([^\)\r\n]+\))",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IAnsiConsole _console;

    /**************************************************************/
    /// <summary>
    /// Initializes the renderer with the console that owns width, color, and output capabilities.
    /// </summary>
    /// <param name="console">The injectable Spectre console used for all help output.</param>
    public MarkdownHelpRenderer(IAnsiConsole console)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(console);
        _console = console;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Parses and renders one complete Markdown help document.
    /// </summary>
    /// <param name="markdown">The trusted repository-authored Markdown document.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="markdown"/> is null.</exception>
    internal void Render(string markdown)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(markdown);

        var paragraphLines = new List<string>();
        var codeLines = new List<string>();
        var inCodeFence = false;

        using var reader = new StringReader(markdown.ReplaceLineEndings("\n"));
        while (reader.ReadLine() is { } line)
        {
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                flushParagraph(paragraphLines);

                if (inCodeFence)
                {
                    renderCodeBlock(codeLines);
                    codeLines.Clear();
                }

                inCodeFence = !inCodeFence;
                continue;
            }

            if (inCodeFence)
            {
                codeLines.Add(line);
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                flushParagraph(paragraphLines);
                continue;
            }

            if (tryRenderHeading(line)
                || tryRenderUnorderedListItem(line)
                || tryRenderOrderedListItem(line))
            {
                flushParagraph(paragraphLines);
                continue;
            }

            paragraphLines.Add(line.Trim());
        }

        flushParagraph(paragraphLines);
        if (codeLines.Count > 0)
        {
            // An unterminated fence remains useful help text and should not crash rendering.
            renderCodeBlock(codeLines);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Renders an ATX heading when the line begins with one to three hash characters.
    /// </summary>
    /// <param name="line">The current Markdown source line.</param>
    /// <returns><see langword="true"/> when the line was rendered as a heading.</returns>
    private bool tryRenderHeading(string line)
    {
        #region implementation

        var trimmed = line.TrimStart();
        var level = 0;
        while (level < trimmed.Length && level < 3 && trimmed[level] == '#')
        {
            level++;
        }

        if (level == 0 || level >= trimmed.Length || trimmed[level] != ' ')
        {
            return false;
        }

        var content = renderInline(trimmed[(level + 1)..].Trim());
        switch (level)
        {
            case 1:
                _console.Write(new Rule($"[bold orange1]{content}[/]")
                    .RuleStyle("grey")
                    .LeftJustified());
                break;
            case 2:
                _console.MarkupLine($"[bold orange1]{content}[/]");
                break;
            default:
                _console.MarkupLine($"[bold]{content}[/]");
                break;
        }

        return true;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Renders a hyphen, plus, or asterisk-prefixed unordered list item.
    /// </summary>
    /// <param name="line">The current Markdown source line.</param>
    /// <returns><see langword="true"/> when an unordered list item was rendered.</returns>
    private bool tryRenderUnorderedListItem(string line)
    {
        #region implementation

        var match = UnorderedListPattern.Match(line);
        if (!match.Success)
        {
            return false;
        }

        _console.MarkupLine($"  [orange1]•[/] {renderInline(match.Groups["content"].Value)}");
        return true;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Renders a numeric Markdown list item while preserving its authored ordinal.
    /// </summary>
    /// <param name="line">The current Markdown source line.</param>
    /// <returns><see langword="true"/> when an ordered list item was rendered.</returns>
    private bool tryRenderOrderedListItem(string line)
    {
        #region implementation

        var match = OrderedListPattern.Match(line);
        if (!match.Success)
        {
            return false;
        }

        var number = Markup.Escape(match.Groups["number"].Value);
        var content = renderInline(match.Groups["content"].Value);
        _console.MarkupLine($"  [orange1]{number}.[/] {content}");
        return true;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Emits buffered paragraph lines as one width-aware Spectre markup paragraph.
    /// </summary>
    /// <param name="paragraphLines">The mutable paragraph-line buffer.</param>
    private void flushParagraph(ICollection<string> paragraphLines)
    {
        #region implementation

        if (paragraphLines.Count == 0)
        {
            return;
        }

        _console.MarkupLine(renderInline(string.Join(' ', paragraphLines)));
        _console.WriteLine();
        paragraphLines.Clear();

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Displays fenced code as literal text in a bordered panel without interpreting markup.
    /// </summary>
    /// <param name="codeLines">The ordered literal source lines inside the fence.</param>
    private void renderCodeBlock(IReadOnlyCollection<string> codeLines)
    {
        #region implementation

        var code = string.Join(Environment.NewLine, codeLines);
        _console.Write(new Panel(new Text(code))
            .Header("[grey]Example[/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle("grey"));
        _console.WriteLine();

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Converts supported inline Markdown tokens to safe Spectre markup.
    /// </summary>
    /// <param name="content">The raw paragraph, heading, or list-item text.</param>
    /// <returns>Spectre markup with every authored literal escaped.</returns>
    private static string renderInline(string content)
    {
        #region implementation

        var output = new StringBuilder();
        var position = 0;
        foreach (Match match in InlineTokenPattern.Matches(content))
        {
            output.Append(Markup.Escape(content[position..match.Index]));
            output.Append(renderInlineToken(match.Value));
            position = match.Index + match.Length;
        }

        output.Append(Markup.Escape(content[position..]));
        return output.ToString();

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Converts one recognized inline Markdown token to safe Spectre markup.
    /// </summary>
    /// <param name="token">The complete inline code, emphasis, or link token.</param>
    /// <returns>The formatted and escaped Spectre markup representation.</returns>
    private static string renderInlineToken(string token)
    {
        #region implementation

        if (token.StartsWith('`'))
        {
            return $"[yellow]{Markup.Escape(token[1..^1])}[/]";
        }

        if (token.StartsWith("**", StringComparison.Ordinal))
        {
            return $"[bold]{Markup.Escape(token[2..^2])}[/]";
        }

        if (token.StartsWith('*'))
        {
            return $"[italic]{Markup.Escape(token[1..^1])}[/]";
        }

        var labelEnd = token.IndexOf("](", StringComparison.Ordinal);
        var label = token[1..labelEnd];
        var uri = token[(labelEnd + 2)..^1];
        return $"[link={Markup.Escape(uri)}]{Markup.Escape(label)}[/] ([grey]{Markup.Escape(uri)}[/])";

        #endregion
    }

    #endregion
}
