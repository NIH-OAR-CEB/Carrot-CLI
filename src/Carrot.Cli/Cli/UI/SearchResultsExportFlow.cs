using Carrot.Cli.ISearch;
using Carrot.Cli.Reporting;
using Spectre.Console;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>Owns interactive path, overwrite, and feedback behavior for iSearch Excel export.</summary>
/// <remarks>The flow shares the processed-results path resolver, suggestion, and persistence safety rules.</remarks>
/// <seealso cref="ISearchResultsExportFlow"/>
/// <seealso cref="ProcessedResultsExportFlow"/>
internal sealed class SearchResultsExportFlow : ISearchResultsExportFlow
{
    #region implementation

    private readonly IAnsiConsole _console;
    private readonly ExcelOutputPathResolver _pathResolver;
    private readonly ExcelOutputPathSuggester _pathSuggester;
    private readonly ISearchResultsExporter _exporter;

    /**************************************************************/
    /// <summary>Initializes iSearch export prompts with shared path and persistence boundaries.</summary>
    /// <param name="console">The interactive console.</param>
    /// <param name="pathResolver">The Excel destination validator.</param>
    /// <param name="pathSuggester">The editable default destination provider.</param>
    /// <param name="exporter">The iSearch workbook exporter.</param>
    public SearchResultsExportFlow(
        IAnsiConsole console,
        ExcelOutputPathResolver pathResolver,
        ExcelOutputPathSuggester pathSuggester,
        ISearchResultsExporter exporter)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(pathResolver);
        ArgumentNullException.ThrowIfNull(pathSuggester);
        ArgumentNullException.ThrowIfNull(exporter);
        _console = console;
        _pathResolver = pathResolver;
        _pathSuggester = pathSuggester;
        _exporter = exporter;

        #endregion
    }

    /**************************************************************/
    /// <summary>Prompts for a workbook destination and saves the retained iSearch pages when approved.</summary>
    /// <param name="session">The current session containing all successfully walked pages.</param>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task representing the complete interaction.</returns>
    public async Task RunAsync(SearchResultPageSession session, CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(session);

        var destination = await ExcelExportInteraction.PromptAsync(
                _console,
                _pathResolver,
                _pathSuggester,
                fileNameStem: null,
                "Excel output path [grey](press Enter to accept the suggested .xlsx file; surrounding quotes are accepted)[/]:",
                outputPath => $"Replace existing workbook [yellow]{Markup.Escape(outputPath)}[/]?",
                "[yellow]Excel export cancelled; the existing workbook was not changed.[/]",
                cancellationToken)
            .ConfigureAwait(false);
        if (destination is null)
        {
            return;
        }

        _console.MarkupLine("[orange1]Saving iSearch results to Excel…[/]");
        var result = await _exporter.SaveAsync(
            new SaveISearchResultsRequest
            {
                Session = session,
                OutputPath = destination.OutputPath,
                Overwrite = destination.Overwrite
            },
            cancellationToken).ConfigureAwait(false);

        if (result.Value is { } savedPath)
        {
            _console.MarkupLine($"[green]iSearch Excel report saved:[/] {Markup.Escape(savedPath)}");
            return;
        }

        foreach (var message in result.Messages)
        {
            _console.MarkupLine($"[red]iSearch Excel export failed:[/] {Markup.Escape(message.Message)}");
        }

        #endregion
    }

    #endregion
}
