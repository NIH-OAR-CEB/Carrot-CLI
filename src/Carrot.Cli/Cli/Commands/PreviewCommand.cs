using Carrot.Cli.Cli.Settings;
using Carrot.Cli.Cli.UI;
using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Carrot.Cli.Processing;
using Spectre.Console.Cli;

namespace Carrot.Cli.Cli.Commands;

/**************************************************************/
/// <summary>
/// Defines the noninteractive request-preview command that never calls the cluster endpoint.
/// </summary>
/// <seealso cref="IDocumentProcessingWorkflow.PreviewAsync"/>
/// <seealso cref="RunSettingsResolver.ResolvePreview"/>
internal sealed class PreviewCommand : AsyncCommand<PreviewSettings>
{
    #region implementation

    private readonly IDocumentProcessingWorkflow _workflow;
    private readonly RunSettingsResolver _settingsResolver;
    private readonly ConsoleReporter _reporter;

    /**************************************************************/
    /// <summary>
    /// Initializes the command with preview, settings, and reporting boundaries.
    /// </summary>
    /// <param name="workflow">The noninteractive preview workflow.</param>
    /// <param name="settingsResolver">The option-to-request translation boundary.</param>
    /// <param name="reporter">The console status and diagnostic reporter.</param>
    /// <exception cref="ArgumentNullException">Thrown when a required dependency is null.</exception>
    public PreviewCommand(
        IDocumentProcessingWorkflow workflow,
        RunSettingsResolver settingsResolver,
        ConsoleReporter reporter)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(settingsResolver);
        ArgumentNullException.ThrowIfNull(reporter);
        _workflow = workflow;
        _settingsResolver = settingsResolver;
        _reporter = reporter;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Resolves settings and writes a request preview without prompting or submitting it for clustering.
    /// </summary>
    /// <param name="context">The Spectre command execution context.</param>
    /// <param name="settings">The preview settings.</param>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task containing the documented preview exit code.</returns>
    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        PreviewSettings settings,
        CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(settings);
        var settingsResult = _settingsResolver.ResolvePreview(settings);
        if (settingsResult.Status == OperationStatus.Failure)
        {
            _reporter.WriteMessages(settingsResult.Messages);
            return ExitCodes.InvalidConfiguration;
        }

        var result = await _workflow
            .PreviewAsync(settingsResult.Value!, cancellationToken)
            .ConfigureAwait(false);
        if (result.ExitCode == ExitCodes.Cancellation)
        {
            return ExitCodes.Cancellation;
        }

        _reporter.WriteRunResult(result, quiet: false);
        return result.ExitCode;

        #endregion
    }

    #endregion
}
