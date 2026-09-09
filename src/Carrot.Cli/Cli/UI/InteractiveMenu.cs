using Carrot.Cli.Processing;
using Spectre.Console;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>
/// Coordinates the selectable main, workflow, and help menus used by human operators.
/// </summary>
/// <remarks>
/// Process Documents, Preview Request, Server Information, and iSearch delegate to focused interactive flows.
/// </remarks>
/// <seealso cref="HelpRenderer"/>
/// <seealso cref="AboutRenderer"/>
/// <seealso cref="ApplicationPreambleRenderer"/>
/// <seealso cref="ISearchFlow"/>
internal sealed class InteractiveMenu
{
    #region implementation

    private static readonly object BackToMainMenu = new();
    private readonly IAnsiConsole _console;
    private readonly ApplicationPreambleRenderer _preambleRenderer;
    private readonly ApplicationFooterRenderer _footerRenderer;
    private readonly HelpRenderer _helpRenderer;
    private readonly AboutRenderer _aboutRenderer;
    private readonly ProcessDocumentsMenu _processDocumentsMenu;
    private readonly InteractivePreviewFlow _interactivePreviewFlow;
    private readonly ServerInformationFlow _serverInformationFlow;
    private readonly ISearchFlow _iSearchFlow;

    /**************************************************************/
    /// <summary>Defines stable actions available from the application main menu.</summary>
    private enum MainMenuChoice
    {
        /**************************************************************/
        /// <summary>Opens document preparation, review, and retained batch actions.</summary>
        Process,

        /**************************************************************/
        /// <summary>Opens the request-preview workflow menu.</summary>
        Preview,

        /**************************************************************/
        /// <summary>Opens the server-configuration workflow menu.</summary>
        ServerInfo,

        /**************************************************************/
        /// <summary>Opens the optional iSearch health, dataset, and query workflow.</summary>
        ISearch,

        /**************************************************************/
        /// <summary>Opens the curated help-topic menu.</summary>
        Help,

        /**************************************************************/
        /// <summary>Displays application and compatibility information.</summary>
        About,

        /**************************************************************/
        /// <summary>Ends the interactive session successfully.</summary>
        Exit
    }

    /**************************************************************/
    /// <summary>
    /// Initializes the main menu with presentation and document-preparation navigation dependencies.
    /// </summary>
    /// <param name="console">The interactive Spectre console.</param>
    /// <param name="preambleRenderer">The renderer for the editable welcome and getting-started content.</param>
    /// <param name="footerRenderer">The shared renderer for application and result-navigation status.</param>
    /// <param name="helpRenderer">The curated Markdown help renderer.</param>
    /// <param name="aboutRenderer">The application-information renderer.</param>
    /// <param name="processDocumentsMenu">The implemented document preparation and review menu.</param>
    /// <param name="interactivePreviewFlow">The independent interactive request-preview flow.</param>
    /// <param name="serverInformationFlow">The endpoint-backed Server Information flow.</param>
    /// <param name="iSearchFlow">The optional iSearch health, discovery, and query flow.</param>
    public InteractiveMenu(
        IAnsiConsole console,
        ApplicationPreambleRenderer preambleRenderer,
        ApplicationFooterRenderer footerRenderer,
        HelpRenderer helpRenderer,
        AboutRenderer aboutRenderer,
        ProcessDocumentsMenu processDocumentsMenu,
        InteractivePreviewFlow interactivePreviewFlow,
        ServerInformationFlow serverInformationFlow,
        ISearchFlow iSearchFlow)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(preambleRenderer);
        ArgumentNullException.ThrowIfNull(footerRenderer);
        ArgumentNullException.ThrowIfNull(helpRenderer);
        ArgumentNullException.ThrowIfNull(aboutRenderer);
        ArgumentNullException.ThrowIfNull(processDocumentsMenu);
        ArgumentNullException.ThrowIfNull(interactivePreviewFlow);
        ArgumentNullException.ThrowIfNull(serverInformationFlow);
        ArgumentNullException.ThrowIfNull(iSearchFlow);

        _console = console;
        _preambleRenderer = preambleRenderer;
        _footerRenderer = footerRenderer;
        _helpRenderer = helpRenderer;
        _aboutRenderer = aboutRenderer;
        _processDocumentsMenu = processDocumentsMenu;
        _interactivePreviewFlow = interactivePreviewFlow;
        _serverInformationFlow = serverInformationFlow;
        _iSearchFlow = iSearchFlow;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Displays menus until the operator exits or the supplied cancellation token is canceled.
    /// </summary>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task containing success, invalid-configuration, or cancellation exit code.</returns>
    internal async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        #region implementation

        if (!_console.Profile.Capabilities.Interactive)
        {
            _console.MarkupLine("[red]Interactive input is unavailable.[/] Run [yellow]carrot-cli --help[/] for command usage.");
            return ExitCodes.InvalidConfiguration;
        }

        _preambleRenderer.Render();
        _footerRenderer.Render(new ApplicationFooterState
        {
            Context = "Main Menu",
            Instruction = "Choose a workflow. Ctrl+C cancels the active operation."
        });

        try
        {
            while (true)
            {
                var choice = await createMainMenuPrompt()
                    .ShowAsync(_console, cancellationToken)
                    .ConfigureAwait(false);

                switch (choice)
                {
                    case MainMenuChoice.Process:
                        await _processDocumentsMenu.RunAsync(cancellationToken)
                            .ConfigureAwait(false);
                        break;
                    case MainMenuChoice.Preview:
                        await _interactivePreviewFlow.RunAsync(cancellationToken)
                            .ConfigureAwait(false);
                        break;
                    case MainMenuChoice.ServerInfo:
                        await runServerInformationMenuAsync(cancellationToken)
                            .ConfigureAwait(false);
                        break;
                    case MainMenuChoice.ISearch:
                        await _iSearchFlow.RunAsync(cancellationToken).ConfigureAwait(false);
                        break;
                    case MainMenuChoice.Help:
                        await runHelpMenuAsync(cancellationToken).ConfigureAwait(false);
                        break;
                    case MainMenuChoice.About:
                        _aboutRenderer.Render();
                        break;
                    case MainMenuChoice.Exit:
                        return ExitCodes.Success;
                    default:
                        throw new InvalidOperationException($"Unsupported main-menu selection: {choice}");
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _console.MarkupLine("[yellow]Cancelled.[/]");
            return ExitCodes.Cancellation;
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Runs the interactive Server Information submenu and its endpoint-backed Execute action.
    /// </summary>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task representing Server Information submenu navigation.</returns>
    private async Task runServerInformationMenuAsync(CancellationToken cancellationToken)
    {
        #region implementation

        while (true)
        {
            var choice = await new SelectionPrompt<ServerInformationChoice>()
                .Title("[bold orange1]Server Information[/]")
                .HighlightStyle(new Style(Color.Black, Color.Orange1))
                .UseConverter(item => item switch
                {
                    ServerInformationChoice.Execute => "Execute",
                    ServerInformationChoice.Help => "Help",
                    ServerInformationChoice.Back => "Back to Main Menu",
                    _ => item.ToString()
                })
                .AddChoices(Enum.GetValues<ServerInformationChoice>())
                .ShowAsync(_console, cancellationToken)
                .ConfigureAwait(false);

            switch (choice)
            {
                case ServerInformationChoice.Execute:
                    await _serverInformationFlow.RunAsync(cancellationToken).ConfigureAwait(false);
                    break;
                case ServerInformationChoice.Help:
                    await _helpRenderer.RenderAsync("server-info", cancellationToken).ConfigureAwait(false);
                    break;
                case ServerInformationChoice.Back:
                    return;
                default:
                    throw new InvalidOperationException($"Unsupported server-information action: {choice}");
            }
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Creates the main selection prompt with labels independent from dispatch identifiers.
    /// </summary>
    /// <returns>The configured main-menu prompt.</returns>
    private static SelectionPrompt<MainMenuChoice> createMainMenuPrompt()
    {
        #region implementation

        return new SelectionPrompt<MainMenuChoice>()
            .Title("[bold orange1]Carrot CLI[/] — Select an option")
            .PageSize(8)
            .HighlightStyle(new Style(Color.Black, Color.Orange1))
            .UseConverter(choice => choice switch
            {
                MainMenuChoice.Process => "Process Documents",
                MainMenuChoice.Preview => "Preview Request",
                MainMenuChoice.ServerInfo => "Server Information",
                MainMenuChoice.ISearch => "iSearch",
                MainMenuChoice.Help => "Help",
                MainMenuChoice.About => "About",
                MainMenuChoice.Exit => "Exit",
                _ => choice.ToString()
            })
            .AddChoices(Enum.GetValues<MainMenuChoice>());

        #endregion
    }


    /**************************************************************/
    /// <summary>Defines the actions in the implemented Server Information submenu.</summary>
    private enum ServerInformationChoice { Execute, Help, Back }

    /**************************************************************/
    /// <summary>
    /// Displays the complete topic picker and returns after the Back selection.
    /// </summary>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task representing completion of help-menu navigation.</returns>
    private async Task runHelpMenuAsync(CancellationToken cancellationToken)
    {
        #region implementation

        var choices = _helpRenderer.Topics.Cast<object>()
            .Append(BackToMainMenu)
            .ToArray();

        while (true)
        {
            var selected = await new SelectionPrompt<object>()
                .Title("[bold orange1]Help Topics[/]")
                .PageSize(10)
                .HighlightStyle(new Style(Color.Black, Color.Orange1))
                .UseConverter(item => ReferenceEquals(item, BackToMainMenu)
                    ? "Back to Main Menu"
                    : ((HelpTopic)item).Title)
                .AddChoices(choices)
                .ShowAsync(_console, cancellationToken)
                .ConfigureAwait(false);

            if (ReferenceEquals(selected, BackToMainMenu))
            {
                return;
            }

            await _helpRenderer.RenderAsync(((HelpTopic)selected).Key, cancellationToken).ConfigureAwait(false);
        }

        #endregion
    }

    #endregion
}
