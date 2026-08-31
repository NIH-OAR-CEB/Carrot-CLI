using System.Globalization;
using Carrot.Cli.Configuration;
using Carrot.Cli.Processing;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>Renders correlated Carrot results as five-document pages with Escape-aware navigation.</summary>
/// <remarks>
/// Category paths and scores retain depth-first server order. Scores use round-trip formatting
/// without display rounding and are meaningful only relative to other clusters in this response.
/// </remarks>
/// <seealso cref="ProcessedDocumentBatch"/>
internal sealed class ProcessedResultsPager
{
    #region implementation

    private readonly IAnsiConsole _console;
    private readonly int _pageSize;

    /**************************************************************/
    /// <summary>Defines stable navigation actions available below every processed page.</summary>
    private enum PageChoice
    {
        /**************************************************************/
        /// <summary>Displays the following page.</summary>
        Next,

        /**************************************************************/
        /// <summary>Displays the preceding page.</summary>
        Previous,

        /**************************************************************/
        /// <summary>Returns to retained prepared-batch actions.</summary>
        Back
    }

    /**************************************************************/
    /// <summary>Initializes processed-result paging with the interactive console and configured page size.</summary>
    /// <param name="console">The console receiving tables and navigation prompts.</param>
    /// <param name="options">The validated shared five-row paging configuration.</param>
    public ProcessedResultsPager(IAnsiConsole console, IOptions<CarrotCliOptions> options)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(options);
        _console = console;
        _pageSize = options.Value.PreparedResultsPageSize;

        #endregion
    }

    /**************************************************************/
    /// <summary>Displays processed pages until Back or Escape returns to batch actions.</summary>
    /// <param name="batch">The latest complete successful in-memory processed batch.</param>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task representing pager navigation.</returns>
    internal async Task ShowAsync(ProcessedDocumentBatch batch, CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(batch);
        var pageCount = Math.Max(1, (batch.Rows.Count + _pageSize - 1) / _pageSize);
        var pageIndex = 0;

        while (true)
        {
            renderPage(batch, pageIndex, pageCount);
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
                .Title($"[bold orange1]Processed Results[/] — Page {pageIndex + 1} of {pageCount}")
                .HighlightStyle(new Style(Color.Black, Color.Orange1))
                .UseConverter(choice => choice switch
                {
                    PageChoice.Next => "Next Page",
                    PageChoice.Previous => "Previous Page",
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
                case PageChoice.Back:
                    return;
                default:
                    throw new InvalidOperationException($"Unsupported processed-results action: {selected}");
            }
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Renders one processed table page and its complete batch summary.</summary>
    /// <param name="batch">The retained processed batch.</param>
    /// <param name="pageIndex">The zero-based page index.</param>
    /// <param name="pageCount">The total page count.</param>
    private void renderPage(ProcessedDocumentBatch batch, int pageIndex, int pageCount)
    {
        #region implementation

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("Carrot #").RightAligned())
            .AddColumn("Source / Title")
            .AddColumn("Status")
            .AddColumn(new TableColumn("Count").RightAligned())
            .AddColumn("Category Paths")
            .AddColumn(new TableColumn("Scores").RightAligned());

        foreach (var row in batch.Rows.Skip(pageIndex * _pageSize).Take(_pageSize))
        {
            var paths = row.Memberships.Count == 0
                ? "—"
                : string.Join(Environment.NewLine, row.Memberships.Select(membership => membership.CategoryPath));
            var scores = row.Memberships.Count == 0
                ? "—"
                : string.Join(
                    Environment.NewLine,
                    row.Memberships.Select(membership => membership.Score?.ToString("R", CultureInfo.InvariantCulture) ?? "—"));
            var sourceAndTitle = $"{row.PreparedDocument.SourceFile.RelativePath}{Environment.NewLine}{row.PreparedDocument.Title}";

            table.AddRow(
                new Text(row.CarrotDocumentIndex.ToString(CultureInfo.InvariantCulture)),
                new Text(sourceAndTitle),
                row.IsAssigned ? new Markup("[green]Assigned[/]") : new Markup("[yellow]Unassigned[/]"),
                new Text(row.Memberships.Count.ToString(CultureInfo.InvariantCulture)),
                new Text(paths),
                new Text(scores));
        }

        _console.Write(table);
        _console.MarkupLine(
            $"[grey]Page {pageIndex + 1}/{pageCount}[/]  "
            + $"[green]Assigned: {batch.AssignedCount:N0}[/]  "
            + $"[yellow]Unassigned: {batch.UnassignedCount:N0}[/]  "
            + $"[grey]Memberships: {batch.MembershipCount:N0}[/]");
        _console.MarkupLine($"[grey]Endpoint: {Markup.Escape(batch.Endpoint.AbsoluteUri)}[/]");
        _console.MarkupLine("[grey]Scores are relative only within this response. No file is written unless Excel export is selected.[/]");
        _console.MarkupLine("[grey]Press Escape to return to Batch Actions.[/]");

        #endregion
    }

    #endregion
}
