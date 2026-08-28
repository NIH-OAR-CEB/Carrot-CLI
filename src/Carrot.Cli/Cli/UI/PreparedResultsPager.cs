using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Carrot.Cli.Processing;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>Renders prepared document rows as compact table pages with Escape-aware navigation.</summary>
/// <remarks>
/// The pager never mutates or disposes a prepared batch. Escape and the explicit Back action both
/// return control to the owning batch-action menu while keeping extracted documents in memory.
/// </remarks>
internal sealed class PreparedResultsPager
{
    #region implementation

    private readonly IAnsiConsole _console;
    private readonly int _pageSize;

    /**************************************************************/
    /// <summary>Defines stable navigation actions available below each rendered table page.</summary>
    private enum PageChoice
    {
        /**************************************************************/
        /// <summary>Displays the preceding page.</summary>
        Previous,

        /**************************************************************/
        /// <summary>Displays the following page.</summary>
        Next,

        /**************************************************************/
        /// <summary>Returns to retained prepared-batch actions.</summary>
        Back
    }

    /**************************************************************/
    /// <summary>Initializes the pager with the console and validated page-size configuration.</summary>
    public PreparedResultsPager(IAnsiConsole console, IOptions<CarrotCliOptions> options)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(options);
        _console = console;
        _pageSize = options.Value.PreparedResultsPageSize;

        #endregion
    }

    /**************************************************************/
    /// <summary>Displays pages until Back or Escape returns to batch actions.</summary>
    /// <param name="result">The retained preparation result and diagnostic messages.</param>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task representing pager navigation.</returns>
    internal async Task ShowAsync(
        OperationResult<PreparedDocumentBatch> result,
        CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(result);
        if (result.Value is null)
        {
            return;
        }

        var rows = result.Value.Rows;
        var pageCount = Math.Max(1, (rows.Count + _pageSize - 1) / _pageSize);
        var pageIndex = 0;

        while (true)
        {
            renderPage(result, pageIndex, pageCount);
            var choices = new List<PageChoice>();
            if (pageIndex > 0)
            {
                choices.Add(PageChoice.Previous);
            }

            if (pageIndex + 1 < pageCount)
            {
                choices.Add(PageChoice.Next);
            }

            choices.Add(PageChoice.Back);
            var selected = await new SelectionPrompt<PageChoice>()
                .Title($"[bold orange1]Prepared Results[/] — Page {pageIndex + 1} of {pageCount}")
                .HighlightStyle(new Style(Color.Black, Color.Orange1))
                .UseConverter(choice => choice switch
                {
                    PageChoice.Previous => "Previous Page",
                    PageChoice.Next => "Next Page",
                    PageChoice.Back => "Back to Batch Actions",
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
                    throw new InvalidOperationException($"Unsupported prepared-results action: {selected}");
            }
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Renders one table page plus stable aggregate counts.</summary>
    private void renderPage(
        OperationResult<PreparedDocumentBatch> result,
        int pageIndex,
        int pageCount)
    {
        #region implementation

        var batch = result.Value!;
        var pageRows = batch.Rows.Skip(pageIndex * _pageSize).Take(_pageSize);
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("#").RightAligned())
            .AddColumn("Status")
            .AddColumn("Source")
            .AddColumn("Type")
            .AddColumn(new TableColumn("Size").RightAligned())
            .AddColumn(new TableColumn("Chars").RightAligned())
            .AddColumn("Content Preview")
            .AddColumn("Message");

        foreach (var row in pageRows)
        {
            table.AddRow(
                new Text((row.SourceOrdinal + 1).ToString()),
                row.Status == PreparedDocumentStatus.Ready
                    ? new Markup("[green]Ready[/]")
                    : new Markup("[red]Failed[/]"),
                new Text(row.RelativePath),
                new Text(row.Extension),
                new Text(formatSize(row.SizeBytes)),
                new Text(row.ExtractedCharacterCount.ToString("N0")),
                new Text(formatPreview(row)),
                new Text(row.ErrorMessage ?? string.Empty));
        }

        _console.Write(table);
        _console.MarkupLine(
            $"[grey]Page {pageIndex + 1}/{pageCount}[/]  "
            + $"[green]Ready: {batch.Documents.Count:N0}[/]  "
            + $"[red]Failed: {batch.FailedCount:N0}[/]  "
            + $"[grey]Total rows: {batch.Rows.Count:N0}[/]");
        _console.MarkupLine("[grey]Press Escape to return to Batch Actions.[/]");

        #endregion
    }

    /**************************************************************/
    /// <summary>Formats one byte count using a compact binary unit.</summary>
    private static string formatSize(long bytes)
    {
        #region implementation

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)bytes;
        var unitIndex = 0;
        while (value >= 1024D && unitIndex < units.Length - 1)
        {
            value /= 1024D;
            unitIndex++;
        }

        return unitIndex == 0 ? $"{bytes:N0} B" : $"{value:0.##} {units[unitIndex]}";

        #endregion
    }

    /**************************************************************/
    /// <summary>Appends a visible ellipsis when a ready row's terminal preview is truncated.</summary>
    private static string formatPreview(PreparedDocumentRow row)
    {
        #region implementation

        if (string.IsNullOrEmpty(row.ContentPreview))
        {
            return string.Empty;
        }

        return row.PreviewTruncated ? $"{row.ContentPreview}…" : row.ContentPreview;

        #endregion
    }

    #endregion
}
