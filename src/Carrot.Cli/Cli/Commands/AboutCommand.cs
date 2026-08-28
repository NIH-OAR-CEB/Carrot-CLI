using Carrot.Cli.Processing;
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

    private readonly AboutRenderer _renderer;

    /**************************************************************/
    /// <summary>
    /// Initializes the command with its about-information renderer.
    /// </summary>
    /// <param name="renderer">The renderer for version and notice information.</param>
    public AboutCommand(AboutRenderer renderer)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(renderer);
        _renderer = renderer;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Renders application and third-party information.
    /// </summary>
    /// <param name="context">The Spectre command execution context.</param>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task containing the about command exit code.</returns>
    protected override Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        #region implementation

        _renderer.Render();
        return Task.FromResult(ExitCodes.Success);

        #endregion
    }

    #endregion
}
