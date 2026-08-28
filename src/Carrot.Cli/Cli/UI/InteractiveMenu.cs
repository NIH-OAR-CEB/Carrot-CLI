using Carrot.Cli.Processing;
using Spectre.Console;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>
/// Coordinates the selectable main, workflow, and help menus used by human operators.
/// </summary>
/// <remarks>
/// Process Documents delegates to its implemented preparation and review coordinator. Preview
/// Request and Server Information retain deferred Execute actions inside their workflow menus.
/// </remarks>
/// <seealso cref="HelpRenderer"/>
/// <seealso cref="AboutRenderer"/>
/// <seealso cref="ApplicationPreambleRenderer"/>
internal sealed class InteractiveMenu
{
    #region implementation

    private static readonly object BackToMainMenu = new();
    private readonly IAnsiConsole _console;
    private readonly ApplicationPreambleRenderer _preambleRenderer;
    private readonly HelpRenderer _helpRenderer;
    private readonly AboutRenderer _aboutRenderer;
    private readonly ProcessDocumentsMenu _processDocumentsMenu;

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
    /// <summary>Defines stable actions shared by each deferred workflow menu.</summary>
    private enum WorkflowMenuChoice
    {
        /**************************************************************/
        /// <summary>Attempts the deferred operation and displays its pending status.</summary>
        Execute,

        /**************************************************************/
        /// <summary>Displays workflow-specific Markdown help.</summary>
        Help,

        /**************************************************************/
        /// <summary>Returns to the application main menu.</summary>
        Back
    }

    /**************************************************************/
    /// <summary>
    /// Initializes the main menu with presentation and document-preparation navigation dependencies.
    /// </summary>
    /// <param name="console">The interactive Spectre console.</param>
    /// <param name="preambleRenderer">The renderer for the editable welcome and getting-started content.</param>
    /// <param name="helpRenderer">The curated Markdown help renderer.</param>
    /// <param name="aboutRenderer">The application-information renderer.</param>
    /// <param name="processDocumentsMenu">The implemented document preparation and review menu.</param>
    public InteractiveMenu(
        IAnsiConsole console,
        ApplicationPreambleRenderer preambleRenderer,
        HelpRenderer helpRenderer,
        AboutRenderer aboutRenderer,
        ProcessDocumentsMenu processDocumentsMenu)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(preambleRenderer);
        ArgumentNullException.ThrowIfNull(helpRenderer);
        ArgumentNullException.ThrowIfNull(aboutRenderer);
        ArgumentNullException.ThrowIfNull(processDocumentsMenu);

        _console = console;
        _preambleRenderer = preambleRenderer;
        _helpRenderer = helpRenderer;
        _aboutRenderer = aboutRenderer;
        _processDocumentsMenu = processDocumentsMenu;

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
                        await runWorkflowMenuAsync("Preview Request", "preview", cancellationToken)
                            .ConfigureAwait(false);
                        break;
                    case MainMenuChoice.ServerInfo:
                        await runWorkflowMenuAsync("Server Information", "server-info", cancellationToken)
                            .ConfigureAwait(false);
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
                MainMenuChoice.Help => "Help",
                MainMenuChoice.About => "About",
                MainMenuChoice.Exit => "Exit",
                _ => choice.ToString()
            })
            .AddChoices(Enum.GetValues<MainMenuChoice>());

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Runs one deferred workflow screen with Execute, Help, and Back selections.
    /// </summary>
    /// <param name="title">The workflow title shown in the prompt and pending panel.</param>
    /// <param name="helpTopic">The canonical topic displayed by the Help selection.</param>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task representing completion of submenu navigation.</returns>
    private async Task runWorkflowMenuAsync(
        string title,
        string helpTopic,
        CancellationToken cancellationToken)
    {
        #region implementation

        while (true)
        {
            var choice = await new SelectionPrompt<WorkflowMenuChoice>()
                .Title($"[bold orange1]{Markup.Escape(title)}[/]")
                .HighlightStyle(new Style(Color.Black, Color.Orange1))
                .UseConverter(item => item switch
                {
                    WorkflowMenuChoice.Execute => "Execute",
                    WorkflowMenuChoice.Help => "Help",
                    WorkflowMenuChoice.Back => "Back to Main Menu",
                    _ => item.ToString()
                })
                .AddChoices(Enum.GetValues<WorkflowMenuChoice>())
                .ShowAsync(_console, cancellationToken)
                .ConfigureAwait(false);

            switch (choice)
            {
                case WorkflowMenuChoice.Execute:
                    _console.Write(new Panel(new Text("Pending Implementation"))
                        .Header($"[orange1]{Markup.Escape(title)}[/]")
                        .Border(BoxBorder.Rounded)
                        .BorderStyle(new Style(Color.Yellow)));
                    _console.WriteLine();
                    break;
                case WorkflowMenuChoice.Help:
                    _helpRenderer.Render(helpTopic);
                    break;
                case WorkflowMenuChoice.Back:
                    return;
                default:
                    throw new InvalidOperationException($"Unsupported workflow-menu selection: {choice}");
            }
        }

        #endregion
    }

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

            _helpRenderer.Render(((HelpTopic)selected).Key);
        }

        #endregion
    }

    #endregion
}
