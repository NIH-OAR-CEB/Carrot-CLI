using Carrot.Cli.Cli.Reporting;
using Carrot.Cli.Cli.Settings;
using Carrot.Cli.Cli.UI;
using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Carrot.Cli.Processing;
using Spectre.Console.Cli;

namespace Carrot.Cli.Cli.Commands;

/**************************************************************/
/// <summary>Defines the noninteractive end-to-end document processing command.</summary>
/// <seealso cref="IDocumentProcessingWorkflow.ProcessAsync"/>
internal sealed class ProcessCommand : AsyncCommand<ProcessSettings>
{
    #region implementation

    private readonly IDocumentProcessingWorkflow _workflow;
    private readonly RunSettingsResolver _settingsResolver;
    private readonly ConsoleReporter _reporter;
    private readonly ICommandRunLogger _logger;

    /**************************************************************/
    /// <summary>Initializes named processing with workflow, settings, reporting, and safe logging boundaries.</summary>
    public ProcessCommand(IDocumentProcessingWorkflow workflow, RunSettingsResolver settingsResolver, ConsoleReporter reporter, ICommandRunLogger logger)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(settingsResolver);
        ArgumentNullException.ThrowIfNull(reporter);
        ArgumentNullException.ThrowIfNull(logger);
        _workflow = workflow;
        _settingsResolver = settingsResolver;
        _reporter = reporter;
        _logger = logger;

        #endregion
    }

    /**************************************************************/
    /// <summary>Resolves options and runs processing without prompting.</summary>
    protected override async Task<int> ExecuteAsync(CommandContext context, ProcessSettings settings, CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(settings);
        var settingsResult = _settingsResolver.ResolveProcess(settings);
        if (settingsResult.Status == OperationStatus.Failure)
        {
            _reporter.WriteMessages(settingsResult.Messages);
            return ExitCodes.InvalidConfiguration;
        }

        var request = settingsResult.Value!;
        try
        {
            await _logger.WriteStartedAsync(request.LogFile, "process", cancellationToken).ConfigureAwait(false);
            var result = await _workflow.ProcessAsync(request, cancellationToken).ConfigureAwait(false);
            if (result.ExitCode == ExitCodes.Cancellation)
            {
                await _logger.WriteCancellationAsync(request.LogFile).ConfigureAwait(false);
                return ExitCodes.Cancellation;
            }

            _reporter.WriteRunResult(result, request.Quiet);
            await _logger.WriteResultAsync(request.LogFile, result, cancellationToken).ConfigureAwait(false);
            return result.ExitCode;
        }
        catch (OperationCanceledException)
        {
            await _logger.WriteCancellationAsync(request.LogFile).ConfigureAwait(false);
            return ExitCodes.Cancellation;
        }

        #endregion
    }

    #endregion
}
