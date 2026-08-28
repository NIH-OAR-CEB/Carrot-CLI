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

    private readonly InteractiveMenu _menu;

    /**************************************************************/
    /// <summary>
    /// Initializes the command with its interactive menu coordinator.
    /// </summary>
    /// <param name="menu">The prompt and menu orchestration boundary.</param>
    public InteractiveCommand(InteractiveMenu menu)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(menu);
        _menu = menu;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Executes the interactive menu and returns its selected workflow exit code.
    /// </summary>
    /// <param name="context">The Spectre command execution context.</param>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task containing the process exit code.</returns>
    protected override Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        #region implementation

        return _menu.RunAsync(cancellationToken);

        #endregion
    }

    #endregion
}
