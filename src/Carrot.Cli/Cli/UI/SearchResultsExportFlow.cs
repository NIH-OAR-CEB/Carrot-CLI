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
            overwrite = await overwritePrompt.ShowAsync(_console, cancellationToken).ConfigureAwait(false);
            if (!overwrite)
            {
                _console.MarkupLine("[yellow]Excel export cancelled; the existing workbook was not changed.[/]");
                return;
            }
        }

        _console.MarkupLine("[orange1]Saving iSearch results to Excel…[/]");
        var result = await _exporter.SaveAsync(
            new SaveISearchResultsRequest
            {
                Session = session,
                OutputPath = outputPath,
                Overwrite = overwrite
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

    /**************************************************************/
    /// <summary>Adapts shared path validation to Spectre.Console prompt validation.</summary>
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
