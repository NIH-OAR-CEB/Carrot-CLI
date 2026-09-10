using Carrot.Cli.Processing;
using Carrot.Cli.Reporting;
using Spectre.Console;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>
/// Owns the interactive path, overwrite, and feedback flow for one retained Excel export.
/// </summary>
/// <remarks>
/// Keeping this interaction outside <see cref="ProcessDocumentsMenu"/> prevents navigation
/// orchestration from acquiring workbook mapping and persistence responsibilities.
/// </remarks>
internal sealed class ProcessedResultsExportFlow
{
    #region implementation

    private readonly IAnsiConsole _console;
    private readonly ExcelOutputPathResolver _pathResolver;
    private readonly ExcelOutputPathSuggester _pathSuggester;
    private readonly IProcessedResultsExporter _exporter;

    /**************************************************************/
    /// <summary>Initializes the flow with console, path-validation, and export boundaries.</summary>
    /// <param name="console">The interactive console used for prompts and feedback.</param>
    /// <param name="pathResolver">The Excel destination validator and normalizer.</param>
    /// <param name="pathSuggester">The provider of an editable default Excel destination.</param>
    /// <param name="exporter">The processed-result persistence boundary.</param>
    public ProcessedResultsExportFlow(
        IAnsiConsole console,
        ExcelOutputPathResolver pathResolver,
        ExcelOutputPathSuggester pathSuggester,
        IProcessedResultsExporter exporter)
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
    /// <summary>Prompts for a workbook destination and saves the supplied retained batch when approved.</summary>
    /// <param name="batch">The latest complete successful processed result.</param>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task representing the complete interaction.</returns>
    internal async Task RunAsync(ProcessedDocumentBatch batch, CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(batch);

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

        _console.MarkupLine("[orange1]Saving processed results to Excel…[/]");
        var result = await _exporter.SaveAsync(
            new SaveProcessedResultsRequest
            {
                Batch = batch,
                OutputPath = destination.OutputPath,
                Overwrite = destination.Overwrite
            },
            cancellationToken).ConfigureAwait(false);

        if (result.Value is { } savedPath)
        {
            _console.MarkupLine($"[green]Excel report saved:[/] {Markup.Escape(savedPath)}");
            return;
        }

        foreach (var message in result.Messages)
        {
            _console.MarkupLine($"[red]Excel export failed:[/] {Markup.Escape(message.Message)}");
        }

        #endregion
    }

    #endregion
}
