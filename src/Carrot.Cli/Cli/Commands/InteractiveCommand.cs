using Carrot.Cli.Cli.UI;
using Spectre.Console.Cli;

namespace Carrot.Cli.Cli.Commands;

/**************************************************************/
/// <summary>
/// Launches the prompt-driven menu used when no command-line arguments are supplied.
/// </summary>
/// <seealso cref="InteractiveMenu"/>
internal sealed class InteractiveCommand : AsyncCommand
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Initializes the command with its interactive menu coordinator.
    /// </summary>
    /// <param name="menu">The prompt and menu orchestration boundary.</param>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    internal InteractiveCommand(InteractiveMenu menu)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Executes the interactive menu and returns its selected workflow exit code.
    /// </summary>
    /// <param name="context">The Spectre command execution context.</param>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task containing the process exit code.</returns>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    protected override Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
