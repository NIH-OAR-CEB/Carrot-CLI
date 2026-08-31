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

        var suggestedPath = _pathSuggester.Suggest();
        var rawPath = await new TextPrompt<string>(
                "Excel output path [grey](press Enter to accept the suggested .xlsx file; surrounding quotes are accepted)[/]:")
            .DefaultValue(suggestedPath)
            .PromptStyle("yellow")
            .Validate(value => validatePath(value))
            .ShowAsync(_console, cancellationToken)
            .ConfigureAwait(false);
        var outputPath = _pathResolver.Resolve(rawPath).Value!;
        var overwrite = false;

        if (File.Exists(outputPath))
        {
            var overwritePrompt = new ConfirmationPrompt(
                $"Replace existing workbook [yellow]{Markup.Escape(outputPath)}[/]?")
            {
                DefaultValue = false
            };
            overwrite = await overwritePrompt
                .ShowAsync(_console, cancellationToken)
                .ConfigureAwait(false);
            if (!overwrite)
            {
                _console.MarkupLine("[yellow]Excel export cancelled; the existing workbook was not changed.[/]");
                return;
            }
        }

        _console.MarkupLine("[orange1]Saving processed results to Excel…[/]");
        var result = await _exporter.SaveAsync(
            new SaveProcessedResultsRequest
            {
                Batch = batch,
                OutputPath = outputPath,
                Overwrite = overwrite
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

    /**************************************************************/
    /// <summary>Adapts structured path validation to Spectre.Console prompt validation.</summary>
    /// <param name="value">The raw prompt value.</param>
    /// <returns>A successful prompt result or the first safe validation message.</returns>
    private ValidationResult validatePath(string value)
    {
        #region implementation

        var result = _pathResolver.Resolve(value);
        return result.Value is null
            ? ValidationResult.Error(result.Messages[0].Message)
            : ValidationResult.Success();

        #endregion
    }

    #endregion
}
