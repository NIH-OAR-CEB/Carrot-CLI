using System.Globalization;
using Carrot.Cli.Configuration;
using Carrot.Cli.Processing;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>Renders correlated Carrot results as five-document pages with export and Escape-aware navigation.</summary>
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
    private readonly ProcessedResultsExportFlow _exportFlow;

    /**************************************************************/
    /// <summary>Initializes processed-result paging with the interactive console, page size, and Excel export flow.</summary>
    /// <param name="console">The console receiving tables and navigation prompts.</param>
    /// <param name="options">The validated shared five-row paging configuration.</param>
    /// <param name="exportFlow">The existing retained-result Excel interaction.</param>
    public ProcessedResultsPager(
        IAnsiConsole console,
        IOptions<CarrotCliOptions> options,
        ProcessedResultsExportFlow exportFlow)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(exportFlow);
        _console = console;
        _pageSize = options.Value.PreparedResultsPageSize;
        _exportFlow = exportFlow;

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
        await PagedResultNavigator.ShowAsync(
                _console,
                batch.Rows.Count,
                _pageSize,
                "Processed Results",
                "Save Processed Results to Excel",
                "Back to Batch Actions",
                selectSaveByDefaultOnFinalPage: true,
                (pageIndex, pageCount) => renderPage(batch, pageIndex, pageCount),
                token => _exportFlow.RunAsync(batch, token),
                cancellationToken)
            .ConfigureAwait(false);

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
