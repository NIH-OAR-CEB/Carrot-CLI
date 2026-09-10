using Carrot.Cli.Reporting;
using Spectre.Console;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>Centralizes the shared interactive Excel destination and overwrite interaction.</summary>
/// <remarks>
/// Export flows retain ownership of source-specific request construction and feedback. This helper
/// owns only path suggestion, validation, overwrite confirmation, and the declined-save message.
/// </remarks>
/// <seealso cref="ExcelExportDestination"/>
/// <seealso cref="ExcelOutputPathResolver"/>
internal static class ExcelExportInteraction
{
    #region implementation

    /**************************************************************/
    /// <summary>Prompts for an approved Excel destination without performing the export.</summary>
    /// <param name="console">The console receiving prompts and cancellation feedback.</param>
    /// <param name="pathResolver">The shared Excel path validator and normalizer.</param>
    /// <param name="pathSuggester">The shared timestamped path suggester.</param>
    /// <param name="fileNameStem">An optional safe filename stem; null preserves the default stem.</param>
    /// <param name="outputPrompt">The operator-facing destination prompt.</param>
    /// <param name="overwritePromptFactory">Creates the overwrite prompt for a normalized path.</param>
    /// <param name="declinedMessage">The message shown when overwrite is declined.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>The approved destination, or null when an existing file is retained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a required text argument is blank.</exception>
    internal static async Task<ExcelExportDestination?> PromptAsync(
        IAnsiConsole console,
        ExcelOutputPathResolver pathResolver,
        ExcelOutputPathSuggester pathSuggester,
        string? fileNameStem,
        string outputPrompt,
        Func<string, string> overwritePromptFactory,
        string declinedMessage,
        CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(pathResolver);
        ArgumentNullException.ThrowIfNull(pathSuggester);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPrompt);
        ArgumentNullException.ThrowIfNull(overwritePromptFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(declinedMessage);

        var suggestedPath = fileNameStem is null
            ? pathSuggester.Suggest()
            : pathSuggester.Suggest(fileNameStem);
        var rawPath = await new TextPrompt<string>(outputPrompt)
            .DefaultValue(suggestedPath)
            .PromptStyle("yellow")
            .Validate(value => validatePath(pathResolver, value))
            .ShowAsync(console, cancellationToken)
            .ConfigureAwait(false);
        var outputPath = pathResolver.Resolve(rawPath).Value!;
        var overwrite = false;

        if (File.Exists(outputPath))
        {
            overwrite = await new ConfirmationPrompt(overwritePromptFactory(outputPath))
            { DefaultValue = false }
                .ShowAsync(console, cancellationToken)
                .ConfigureAwait(false);
            if (!overwrite)
            {
                console.MarkupLine(declinedMessage);
                return null;
            }
        }

        return new ExcelExportDestination
        {
            OutputPath = outputPath,
            Overwrite = overwrite
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>Adapts shared path validation to Spectre prompt validation.</summary>
    /// <param name="pathResolver">The shared Excel path validator.</param>
    /// <param name="value">The raw path supplied by the operator.</param>
    /// <returns>A successful validation result or the first safe error.</returns>
    private static ValidationResult validatePath(ExcelOutputPathResolver pathResolver, string value)
    {
        #region implementation

        var result = pathResolver.Resolve(value);
        return result.Value is null
            ? ValidationResult.Error(result.Messages[0].Message)
            : ValidationResult.Success();

        #endregion
    }

    #endregion
}
