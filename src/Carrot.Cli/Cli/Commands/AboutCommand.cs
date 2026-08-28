using Carrot.Cli.Cli.UI;
using Spectre.Console.Cli;

namespace Carrot.Cli.Cli.Commands;

/**************************************************************/
/// <summary>
/// Defines the command that displays application, compatibility, and notice information.
/// </summary>
/// <seealso cref="AboutRenderer"/>
internal sealed class AboutCommand : AsyncCommand
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Initializes the command with its about-information renderer.
    /// </summary>
    /// <param name="renderer">The renderer for version and notice information.</param>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    internal AboutCommand(AboutRenderer renderer)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Renders application and third-party information.
    /// </summary>
    /// <param name="context">The Spectre command execution context.</param>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task containing the about command exit code.</returns>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    protected override Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
