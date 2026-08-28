using Carrot.Cli.Cli.Settings;
using Carrot.Cli.Cli.UI;
using Carrot.Cli.Processing;
using Spectre.Console.Cli;

namespace Carrot.Cli.Cli.Commands;

/**************************************************************/
/// <summary>
/// Defines the noninteractive request-preview command that never calls the cluster endpoint.
/// </summary>
/// <seealso cref="IDocumentProcessingWorkflow.PreviewAsync"/>
internal sealed class PreviewCommand : AsyncCommand<PreviewSettings>
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Initializes the command with workflow and reporting boundaries.
    /// </summary>
    /// <param name="workflow">The preview-capable document workflow.</param>
    /// <param name="reporter">The console status and diagnostic reporter.</param>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    internal PreviewCommand(IDocumentProcessingWorkflow workflow, ConsoleReporter reporter)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Resolves settings and writes a request preview without submitting it.
    /// </summary>
    /// <param name="context">The Spectre command execution context.</param>
    /// <param name="settings">The validated preview settings.</param>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task containing the documented preview exit code.</returns>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    protected override Task<int> ExecuteAsync(CommandContext context, PreviewSettings settings, CancellationToken cancellationToken)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
