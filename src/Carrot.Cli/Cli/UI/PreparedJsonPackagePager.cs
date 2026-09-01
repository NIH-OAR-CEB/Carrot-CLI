using System.Text;
using System.Text.Json;
using Carrot.Cli.CarrotApi;
using Carrot.Cli.CarrotApi.Contracts;
using Carrot.Cli.Processing;
using Spectre.Console;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>Renders the pretty-printed JSON request package through bounded interactive pages.</summary>
/// <remarks>
/// Paging is local and read-only: it does not contact Carrot or write an artifact. The exact
/// indented serialization is visually wrapped to prevent one full-content JSON string from
/// expanding into a giant terminal dump. A continuation marker is display-only.
/// </remarks>
/// <seealso cref="ClusterRequestFactory"/>
/// <seealso cref="PreparedDocumentBatch"/>
internal sealed class PreparedJsonPackagePager
{
    #region implementation

    private const int MinimumPageLineCount = 5;
    private const int MaximumPageLineCount = 30;
    private const int ReservedTerminalRows = 12;
    private const int MinimumWrapWidth = 30;
    private const int MaximumWrapWidth = 120;
    private const int PanelWidthAllowance = 8;
    private const string ContinuationPrefix = "↪ ";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly IAnsiConsole _console;
    private readonly ClusterRequestFactory _requestFactory;

    /**************************************************************/
    /// <summary>Defines navigation actions below each JSON page.</summary>
    private enum PageChoice
    {
        /**************************************************************/
        /// <summary>Displays the following JSON page.</summary>
        Next,

        /**************************************************************/
        /// <summary>Displays the preceding JSON page.</summary>
        Previous,

        /**************************************************************/
        /// <summary>Runs the caller-supplied explicit request-save action.</summary>
        Save,

        /**************************************************************/
        /// <summary>Runs the caller-supplied explicit Workbench dataset-save action.</summary>
        SaveWorkbench,

        /**************************************************************/
        /// <summary>Returns to retained prepared-batch actions.</summary>
        Back
    }

    /**************************************************************/
    /// <summary>Initializes the pager with its console and shared wire-contract factory.</summary>
    /// <param name="console">The console that receives JSON pages and navigation prompts.</param>
    /// <param name="requestFactory">The shared prepared-document request mapper.</param>
    public PreparedJsonPackagePager(IAnsiConsole console, ClusterRequestFactory requestFactory)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(requestFactory);
        _console = console;
        _requestFactory = requestFactory;

        #endregion
    }

    /**************************************************************/
    /// <summary>Displays pretty-printed JSON pages until Back or Escape returns to batch actions.</summary>
    /// <param name="batch">The retained batch whose successful documents form the request.</param>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task representing JSON-page navigation.</returns>
    /// <remarks>
    /// Next Page is the first and default action whenever a following page exists. The page size
    /// and wrap width adapt to the active console profile while retaining conservative bounds.
    /// </remarks>
    internal async Task ShowAsync(PreparedDocumentBatch batch, CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(batch);
        var request = _requestFactory.Create(batch.Documents);
        await ShowAsync(request, batch.Documents.Count, saveAction: null, saveWorkbenchAction: null, cancellationToken).ConfigureAwait(false);

        #endregion
    }

    /**************************************************************/
    /// <summary>Displays an already validated request and optionally exposes an explicit save action.</summary>
    /// <param name="request">The exact request to display without further transformation.</param>
    /// <param name="documentCount">The number of ready documents represented by the request.</param>
    /// <param name="saveAction">The optional explicit persistence action, or null for read-only previews.</param>
    /// <param name="saveWorkbenchAction">The optional explicit Workbench dataset persistence action.</param>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task representing JSON-page navigation.</returns>
    internal async Task ShowAsync(
        ClusterRequest request,
        int documentCount,
        Func<CancellationToken, Task>? saveAction,
        Func<CancellationToken, Task>? saveWorkbenchAction,
        CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(request);
        var json = JsonSerializer.Serialize(request, SerializerOptions);
        var pageLineCount = Math.Clamp(
            _console.Profile.Height - ReservedTerminalRows,
            MinimumPageLineCount,
            MaximumPageLineCount);
        var wrapWidth = Math.Clamp(
            _console.Profile.Width - PanelWidthAllowance,
            MinimumWrapWidth,
            MaximumWrapWidth);
        var visualLines = createVisualLines(json, wrapWidth);
        var pageCount = Math.Max(1, (visualLines.Count + pageLineCount - 1) / pageLineCount);
        var pageIndex = 0;

        while (true)
        {
            renderPage(json, visualLines, documentCount, pageIndex, pageCount, pageLineCount);
            var choices = new List<PageChoice>();
            if (pageIndex + 1 < pageCount)
            {
                choices.Add(PageChoice.Next);
            }

            if (pageIndex > 0)
            {
                choices.Add(PageChoice.Previous);
            }

            if (saveAction is not null)
            {
                choices.Add(PageChoice.Save);
            }

            if (saveWorkbenchAction is not null)
            {
                choices.Add(PageChoice.SaveWorkbench);
            }

            choices.Add(PageChoice.Back);
            var selected = await new SelectionPrompt<PageChoice>()
                .Title($"[bold orange1]JSON Request Package[/] — Page {pageIndex + 1} of {pageCount}")
                .HighlightStyle(new Style(Color.Black, Color.Orange1))
                .UseConverter(choice => choice switch
                {
                    PageChoice.Next => "Next Page",
                    PageChoice.Previous => "Previous Page",
                    PageChoice.Save => "Save Request JSON",
                    PageChoice.SaveWorkbench => "Save Workbench JSON",
                    PageChoice.Back => "Back to Batch Actions",
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
                case PageChoice.Save:
                    await saveAction!(cancellationToken).ConfigureAwait(false);
                    break;
                case PageChoice.SaveWorkbench:
                    await saveWorkbenchAction!(cancellationToken).ConfigureAwait(false);
                    break;
                case PageChoice.Back:
                    return;
                default:
                    throw new InvalidOperationException($"Unsupported JSON preview action: {selected}");
            }
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Indexes indented JSON as logical and visually wrapped display lines.</summary>
    /// <param name="json">The complete indented JSON serialization.</param>
    /// <param name="wrapWidth">The maximum source characters shown on one visual line.</param>
    /// <returns>Source spans and continuation flags without duplicating the complete JSON text.</returns>
    /// <remarks>
    /// Span indexes avoid allocating a second full-size copy of a potentially large request.
    /// Surrogate pairs are kept together when a visual boundary falls inside a Unicode scalar.
    /// </remarks>
    private static IReadOnlyList<(int Start, int Length, bool IsContinuation)> createVisualLines(
        string json,
        int wrapWidth)
    {
        #region implementation

        var lines = new List<(int Start, int Length, bool IsContinuation)>();
        var logicalLineStart = 0;
        for (var index = 0; index <= json.Length; index++)
        {
            if (index < json.Length && json[index] != '\n')
            {
                continue;
            }

            var logicalLineLength = index - logicalLineStart;
            if (logicalLineLength > 0 && json[logicalLineStart + logicalLineLength - 1] == '\r')
            {
                logicalLineLength--;
            }

            if (logicalLineLength == 0)
            {
                lines.Add((logicalLineStart, 0, false));
            }
            else
            {
                var offset = 0;
                while (offset < logicalLineLength)
                {
                    var segmentLength = Math.Min(wrapWidth, logicalLineLength - offset);
                    var segmentEnd = logicalLineStart + offset + segmentLength;
                    if (segmentEnd < logicalLineStart + logicalLineLength
                        && char.IsHighSurrogate(json[segmentEnd - 1])
                        && char.IsLowSurrogate(json[segmentEnd]))
                    {
                        segmentLength--;
                    }

                    lines.Add((logicalLineStart + offset, segmentLength, offset > 0));
                    offset += segmentLength;
                }
            }

            logicalLineStart = index + 1;
        }

        return lines;

        #endregion
    }

    /**************************************************************/
    /// <summary>Renders one bounded page plus page, line, and document counts.</summary>
    /// <param name="json">The complete indented JSON serialization.</param>
    /// <param name="visualLines">The indexed logical and wrapped display lines.</param>
    /// <param name="documentCount">The number of ready documents represented by the request.</param>
    /// <param name="pageIndex">The zero-based page being rendered.</param>
    /// <param name="pageCount">The total number of display pages.</param>
    /// <param name="pageLineCount">The maximum visual lines rendered on one page.</param>
    private void renderPage(
        string json,
        IReadOnlyList<(int Start, int Length, bool IsContinuation)> visualLines,
        int documentCount,
        int pageIndex,
        int pageCount,
        int pageLineCount)
    {
        #region implementation

        var firstLineIndex = pageIndex * pageLineCount;
        var lastLineIndexExclusive = Math.Min(firstLineIndex + pageLineCount, visualLines.Count);
        var pageText = new StringBuilder();
        for (var index = firstLineIndex; index < lastLineIndexExclusive; index++)
        {
            var line = visualLines[index];
            if (line.IsContinuation)
            {
                pageText.Append(ContinuationPrefix);
            }

            pageText.Append(json.AsSpan(line.Start, line.Length));
            if (index + 1 < lastLineIndexExclusive)
            {
                pageText.AppendLine();
            }
        }

        _console.Write(new Panel(new Text(pageText.ToString()))
            .Header($"[orange1]JSON Request Package[/] — {documentCount:N0} document(s)")
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(Color.Orange1)));
        _console.MarkupLine(
            $"[grey]Page {pageIndex + 1}/{pageCount}[/]  "
            + $"[grey]Lines {firstLineIndex + 1:N0}-{lastLineIndexExclusive:N0} of {visualLines.Count:N0}[/]");
        _console.MarkupLine(
            $"[grey]{Markup.Escape(ContinuationPrefix)} marks a visually wrapped continuation; the JSON value is unchanged.[/]");
        _console.MarkupLine("[grey]Press Escape to return to Batch Actions.[/]");
        _console.MarkupLine("[yellow]Preview only:[/] no server request was sent and no cluster request was sent.");

        #endregion
    }

    #endregion
}
