using Carrot.Cli.Cli.Settings;
using Carrot.Cli.Cli.UI;
using Carrot.Cli.Processing;
using Spectre.Console.Cli;

namespace Carrot.Cli.Cli.Commands;

/**************************************************************/
/// <summary>
/// Defines the noninteractive end-to-end document processing command.
/// </summary>
/// <seealso cref="IDocumentProcessingWorkflow"/>
internal sealed class ProcessCommand : AsyncCommand<ProcessSettings>
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Initializes the command with workflow and reporting boundaries.
    /// </summary>
    /// <param name="workflow">The end-to-end processing workflow.</param>
    /// <param name="reporter">The console status and diagnostic reporter.</param>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    internal ProcessCommand(IDocumentProcessingWorkflow workflow, ConsoleReporter reporter)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Resolves settings and runs processing without prompting.
    /// </summary>
    /// <param name="context">The Spectre command execution context.</param>
    /// <param name="settings">The validated command settings.</param>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task containing the documented processing exit code.</returns>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    protected override Task<int> ExecuteAsync(CommandContext context, ProcessSettings settings, CancellationToken cancellationToken)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
