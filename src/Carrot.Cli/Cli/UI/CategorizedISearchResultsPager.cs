using System.Globalization;
using System.Text.Json;
using Carrot.Cli.Configuration;
using Carrot.Cli.ISearch;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>Displays categorized iSearch rows and exposes explicit categorized Excel export.</summary>
/// <remarks>
/// The pager consumes only the completed categorized batch. It does not fetch data or recategorize
/// records when the operator moves between display pages or chooses Save.
/// </remarks>
/// <seealso cref="CategorizedISearchResultBatch"/>
internal sealed class CategorizedISearchResultsPager : ICategorizedISearchResultsPager
{
    #region implementation

    private readonly IAnsiConsole _console;
    private readonly int _pageSize;
    private readonly ICategorizedISearchResultsExportFlow _exportFlow;

    /**************************************************************/
    /// <summary>Initializes categorized-result paging.</summary>
    /// <param name="console">The interactive console.</param>
    /// <param name="options">The validated result page-size options.</param>
    /// <param name="exportFlow">The categorized Excel export interaction.</param>
    public CategorizedISearchResultsPager(
        IAnsiConsole console,
        IOptions<CarrotCliOptions> options,
        ICategorizedISearchResultsExportFlow exportFlow)
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
    /// <summary>Displays categorized rows until the operator returns to iSearch results.</summary>
    /// <param name="batch">The completed categorized iSearch batch.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    public async Task ShowAsync(CategorizedISearchResultBatch batch, CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(batch);
        await PagedResultNavigator.ShowAsync(
                _console,
                batch.Rows.Count,
                _pageSize,
                "Categorized iSearch Results",
                "Save Categorized iSearch Results to Excel",
                "Back to iSearch Results",
                selectSaveByDefaultOnFinalPage: false,
                (pageIndex, pageCount) => renderPage(batch, pageIndex, pageCount),
                token => _exportFlow.RunAsync(batch, token),
                cancellationToken)
            .ConfigureAwait(false);

        #endregion
    }

    /**************************************************************/
    /// <summary>Renders one page of categorized iSearch records.</summary>
    /// <param name="batch">The categorized batch.</param>
    /// <param name="pageIndex">The zero-based page index.</param>
    /// <param name="pageCount">The total display-page count.</param>
    private void renderPage(CategorizedISearchResultBatch batch, int pageIndex, int pageCount)
    {
        #region implementation

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("Result #").RightAligned())
            .AddColumn("NIH Application")
            .AddColumn("Title")
            .AddColumn("Status")
            .AddColumn(new TableColumn("Count").RightAligned())
            .AddColumn("Category Paths")
            .AddColumn("Scores");
        foreach (var row in batch.Rows.Skip(pageIndex * _pageSize).Take(_pageSize))
        {
            var paths = row.Memberships.Count == 0
                ? "—"
                : string.Join(Environment.NewLine, row.Memberships.Select(membership => membership.CategoryPath));
            var scores = row.Memberships.Count == 0
                ? "—"
                : string.Join(Environment.NewLine, row.Memberships.Select(membership =>
                    membership.Score?.ToString("R", CultureInfo.InvariantCulture) ?? "—"));
            table.AddRow(
                new Text(row.ResultOrdinal.ToString(CultureInfo.InvariantCulture)),
                new Text(formatValue(row.NihApplId)),
                new Text(formatValue(row.Title)),
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
        _console.MarkupLine($"[grey]Loaded iSearch records: {batch.Rows.Count:N0}[/]");
        _console.MarkupLine("[grey]Press Escape to return to iSearch results.[/]");

        #endregion
    }

    /**************************************************************/
    /// <summary>Formats a JSON scalar as literal display text.</summary>
    /// <param name="value">The source field value.</param>
    /// <returns>A safe human-readable value.</returns>
    private static string formatValue(JsonElement value)
    {
        #region implementation

        return value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => "—",
            JsonValueKind.String => value.GetString() ?? string.Empty,
            _ => value.GetRawText()
        };

        #endregion
    }

    #endregion
}
