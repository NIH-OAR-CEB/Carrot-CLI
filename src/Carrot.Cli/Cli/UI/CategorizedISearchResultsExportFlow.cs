using Carrot.Cli.ISearch;
using Carrot.Cli.Reporting;
using Spectre.Console;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>Owns path, overwrite, and feedback interaction for categorized iSearch Excel output.</summary>
/// <remarks>The flow shares the repository's validated path and atomic writer boundaries.</remarks>
/// <seealso cref="ICategorizedISearchResultsExportFlow"/>
internal sealed class CategorizedISearchResultsExportFlow : ICategorizedISearchResultsExportFlow
{
    #region implementation

    private readonly IAnsiConsole _console;
    private readonly ExcelOutputPathResolver _pathResolver;
    private readonly ExcelOutputPathSuggester _pathSuggester;
    private readonly ICategorizedISearchResultsExporter _exporter;

    /**************************************************************/
    /// <summary>Initializes categorized iSearch export interaction.</summary>
    /// <param name="console">The interactive console.</param>
    /// <param name="pathResolver">The shared Excel destination validator.</param>
    /// <param name="pathSuggester">The timestamped destination suggester.</param>
    /// <param name="exporter">The categorized workbook exporter.</param>
    public CategorizedISearchResultsExportFlow(
        IAnsiConsole console,
        ExcelOutputPathResolver pathResolver,
        ExcelOutputPathSuggester pathSuggester,
        ICategorizedISearchResultsExporter exporter)
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
    /// <summary>Prompts for a workbook path and saves the completed categorized batch.</summary>
    /// <param name="batch">The categorized iSearch data to write.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    public async Task RunAsync(CategorizedISearchResultBatch batch, CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(batch);
        var destination = await ExcelExportInteraction.PromptAsync(
                _console,
                _pathResolver,
                _pathSuggester,
                "carrot-isearch-categorized",
                "Categorized Excel output path [grey](quotes around the .xlsx path are accepted)[/]:",
                outputPath => $"Replace existing categorized workbook [yellow]{Markup.Escape(outputPath)}[/]?",
                "[yellow]Categorized Excel export cancelled; the existing workbook was not changed.[/]",
                cancellationToken)
            .ConfigureAwait(false);
        if (destination is null)
        {
            return;
        }

        _console.MarkupLine("[orange1]Saving categorized iSearch results to Excel…[/]");
        var result = await _exporter.SaveAsync(
            new SaveCategorizedISearchResultsRequest
            {
                Batch = batch,
                OutputPath = destination.OutputPath,
                Overwrite = destination.Overwrite
            },
            cancellationToken).ConfigureAwait(false);
        if (result.Value is { } savedPath)
        {
            _console.MarkupLine($"[green]Categorized iSearch Excel report saved:[/] {Markup.Escape(savedPath)}");
            return;
        }

        foreach (var message in result.Messages)
        {
            _console.MarkupLine($"[red]Categorized iSearch Excel export failed:[/] {Markup.Escape(message.Message)}");
        }

        #endregion
    }

    #endregion
}
