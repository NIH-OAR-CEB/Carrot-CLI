using Carrot.Cli.Processing;
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

    private readonly HelpRenderer _renderer;

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
    public HelpCommand(HelpRenderer renderer)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(renderer);
        _renderer = renderer;

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
    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        HelpSettings settings,
        CancellationToken cancellationToken)
    {
        #region implementation

        var rendered = await _renderer.RenderAsync(settings.Topic, cancellationToken).ConfigureAwait(false);
        return rendered ? ExitCodes.Success : ExitCodes.InvalidConfiguration;

        #endregion
    }

    #endregion
}
