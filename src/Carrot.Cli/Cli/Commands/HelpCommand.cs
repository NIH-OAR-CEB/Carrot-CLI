using Carrot.Cli.Cli.UI;
using Spectre.Console.Cli;

namespace Carrot.Cli.Cli.Commands;

/**************************************************************/
/// <summary>
/// Defines topic-oriented application help beyond Spectre's generated option help.
/// </summary>
/// <seealso cref="HelpRenderer"/>
internal sealed class HelpCommand : AsyncCommand<HelpCommand.HelpSettings>
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Defines the optional help topic argument.
    /// </summary>
    internal sealed class HelpSettings : CommandSettings
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Gets or initializes the requested help topic.
        /// </summary>
        [CommandArgument(0, "[TOPIC]")]
        public string? Topic { get; init; }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Initializes the command with its help renderer.
    /// </summary>
    /// <param name="renderer">The renderer for curated help topics.</param>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    internal HelpCommand(HelpRenderer renderer)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Renders the requested help topic.
    /// </summary>
    /// <param name="context">The Spectre command execution context.</param>
    /// <param name="settings">The optional topic selection.</param>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task containing the help command exit code.</returns>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    protected override Task<int> ExecuteAsync(CommandContext context, HelpSettings settings, CancellationToken cancellationToken)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
