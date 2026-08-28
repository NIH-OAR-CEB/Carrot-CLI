using Carrot.Cli.Cli.UI;
using Carrot.Cli.CarrotApi;
using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Carrot.Cli.Input;
using Carrot.Cli.Processing;
using Microsoft.Extensions.Options;
using Spectre.Console.Testing;
using Xunit;

namespace Carrot.Cli.Tests.Cli;

/**************************************************************/
/// <summary>
/// Verifies interactive menu navigation without resolving any operational layout stub.
/// </summary>
public sealed class InteractiveMenuTests
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Verifies the welcome and getting-started preamble is rendered before the first main menu.
    /// </summary>
    [Fact]
    public async Task RunAsync_InteractiveConsole_RendersPreambleBeforeMainMenu()
    {
        #region implementation

        // Arrange
        using var console = createInteractiveConsole();
        pushDownKeys(console, 5);
        console.Input.PushKey(ConsoleKey.Enter);
        var menu = createMenu(console);

        // Act
        var exitCode = await menu.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(ExitCodes.Success, exitCode);
        var preambleIndex = console.Output.IndexOf("Welcome to Carrot CLI", StringComparison.Ordinal);
        var menuIndex = console.Output.IndexOf("Select an option", StringComparison.Ordinal);
        Assert.True(preambleIndex >= 0, "The application preamble was not rendered.");
        Assert.True(menuIndex > preambleIndex, "The application preamble must precede the main menu.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies each workflow supports Execute, Help, and Back and reports the exact pending status.
    /// </summary>
    /// <param name="mainMenuOffset">The number of down-arrow inputs needed to select the workflow.</param>
    /// <param name="workflowTitle">The expected workflow title.</param>
    [Theory]
    [InlineData(1, "Preview Request")]
    [InlineData(2, "Server Information")]
    public async Task RunAsync_WorkflowExecute_WritesPendingAndReturnsToMain(
        int mainMenuOffset,
        string workflowTitle)
    {
        #region implementation

        // Arrange
        using var console = createInteractiveConsole();
        pushDownKeys(console, mainMenuOffset);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);
        pushDownKeys(console, 2);
        console.Input.PushKey(ConsoleKey.Enter);
        pushDownKeys(console, 5);
        console.Input.PushKey(ConsoleKey.Enter);
        var menu = createMenu(console);

        // Act
        var exitCode = await menu.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains(workflowTitle, console.Output, StringComparison.Ordinal);
        Assert.Contains("Execute", console.Output, StringComparison.Ordinal);
        Assert.Contains("Help", console.Output, StringComparison.Ordinal);
        Assert.Contains("Back to Main Menu", console.Output, StringComparison.Ordinal);
        Assert.Contains("Pending Implementation", console.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies Process Documents opens editable path setup instead of the deferred workflow panel.</summary>
    [Fact]
    public async Task RunAsync_ProcessDocuments_OpensInputSetupAndReturnsToMain()
    {
        #region implementation

        // Arrange
        using var console = createInteractiveConsole();
        console.Input.PushKey(ConsoleKey.Enter);
        pushDownKeys(console, 2);
        console.Input.PushKey(ConsoleKey.Enter);
        pushDownKeys(console, 5);
        console.Input.PushKey(ConsoleKey.Enter);
        var menu = createMenu(console);

        // Act
        var exitCode = await menu.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("Build an input batch", console.Output, StringComparison.Ordinal);
        Assert.Contains("Add Path", console.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Pending Implementation", console.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies a workflow Help selection renders focused Markdown and returns to that workflow menu.
    /// </summary>
    [Fact]
    public async Task RunAsync_WorkflowHelp_RendersTopicAndReturnsToWorkflow()
    {
        #region implementation

        // Arrange
        using var console = createInteractiveConsole();
        console.Input.PushKey(ConsoleKey.Enter);
        pushDownKeys(console, 1);
        console.Input.PushKey(ConsoleKey.Enter);
        pushDownKeys(console, 2);
        console.Input.PushKey(ConsoleKey.Enter);
        pushDownKeys(console, 5);
        console.Input.PushKey(ConsoleKey.Enter);
        var menu = createMenu(console);

        // Act
        var exitCode = await menu.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("Process Documents", console.Output, StringComparison.Ordinal);
        Assert.Contains("Build the input batch", console.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies the main Help selection opens the topic picker and returns to the main menu.
    /// </summary>
    [Fact]
    public async Task RunAsync_MainHelp_RendersSelectedTopicAndReturnsToMain()
    {
        #region implementation

        // Arrange
        using var console = createInteractiveConsole();
        pushDownKeys(console, 3);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);
        pushDownKeys(console, 13);
        console.Input.PushKey(ConsoleKey.Enter);
        pushDownKeys(console, 5);
        console.Input.PushKey(ConsoleKey.Enter);
        var menu = createMenu(console);

        // Act
        var exitCode = await menu.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("Help Topics", console.Output, StringComparison.Ordinal);
        Assert.Contains("Getting Started", console.Output, StringComparison.Ordinal);
        Assert.Contains("Run carrot-cli without arguments", console.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies no-argument interactive execution fails quickly when input is redirected.
    /// </summary>
    [Fact]
    public async Task RunAsync_NonInteractiveConsole_ReturnsInvalidConfiguration()
    {
        #region implementation

        // Arrange
        using var console = new TestConsole();
        console.Profile.Capabilities.Interactive = false;
        var menu = createMenu(console);

        // Act
        var exitCode = await menu.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(ExitCodes.InvalidConfiguration, exitCode);
        Assert.Contains("Interactive input is unavailable", console.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies cooperative cancellation maps to the stable cancellation exit code.
    /// </summary>
    [Fact]
    public async Task RunAsync_CanceledToken_ReturnsCancellation()
    {
        #region implementation

        // Arrange
        using var console = createInteractiveConsole();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var menu = createMenu(console);

        // Act
        var exitCode = await menu.RunAsync(cancellationSource.Token);

        // Assert
        Assert.Equal(ExitCodes.Cancellation, exitCode);
        Assert.Contains("Cancelled", console.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Creates a fully wired menu using the real embedded help and rendering implementations.
    /// </summary>
    /// <param name="console">The interactive test console.</param>
    /// <returns>The menu under test.</returns>
    private static InteractiveMenu createMenu(TestConsole console)
    {
        #region implementation

        var catalog = new HelpTopicCatalog();
        var markdownRenderer = new MarkdownHelpRenderer(console);
        var helpRenderer = new HelpRenderer(
            console,
            catalog,
            new EmbeddedHelpContentProvider(),
            markdownRenderer);
        var preambleRenderer = new ApplicationPreambleRenderer(
            console,
            new StubApplicationPreambleProvider(),
            markdownRenderer);
        var aboutRenderer = new AboutRenderer(console);
        var options = Options.Create(new CarrotCliOptions());
        var formats = new DocumentFormatCatalog();
        var folderLoader = new FolderInputSourceLoader(formats, options);
        var fileLoader = new FileInputSourceLoader(formats, options);
        var zipLoader = new ZipInputSourceLoader(formats, options);
        var resolver = new InputSourceResolver(folderLoader, fileLoader, zipLoader, formats);
        var pathNormalizer = new InputPathNormalizer();
        var pager = new PreparedResultsPager(console, options);
        var processDocumentsMenu = new ProcessDocumentsMenu(
            console,
            helpRenderer,
            pathNormalizer,
            resolver,
            new StubDocumentPreparationWorkflow(),
            pager,
            new PreparedJsonPackagePager(console, new ClusterRequestFactory(options)));
        return new InteractiveMenu(console, preambleRenderer, helpRenderer, aboutRenderer, processDocumentsMenu);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Creates a color-capable test console that accepts queued selection-prompt input.
    /// </summary>
    /// <returns>The configured interactive test console.</returns>
    private static TestConsole createInteractiveConsole()
    {
        #region implementation

        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        return console;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Queues a deterministic number of down-arrow inputs for a selection prompt.
    /// </summary>
    /// <param name="console">The console whose input queue is populated.</param>
    /// <param name="count">The number of down-arrow inputs to enqueue.</param>
    private static void pushDownKeys(TestConsole console, int count)
    {
        #region implementation

        for (var index = 0; index < count; index++)
        {
            console.Input.PushKey(ConsoleKey.DownArrow);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Supplies stable welcome content while interactive navigation behavior is under test.
    /// </summary>
    private sealed class StubApplicationPreambleProvider : IApplicationPreambleProvider
    {
        #region implementation

        /**************************************************************/
        /// <summary>Returns deterministic Markdown for the menu's startup preamble.</summary>
        /// <returns>Welcome and getting-started Markdown.</returns>
        public string Read()
        {
            #region implementation

            return "# Welcome to Carrot CLI\n\nSelect a menu option to get started.";

            #endregion
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Supplies a deterministic unused preparation result for main-menu navigation tests.</summary>
    private sealed class StubDocumentPreparationWorkflow : IDocumentPreparationWorkflow
    {
        #region implementation

        /**************************************************************/
        /// <summary>Returns an empty failed batch if a navigation test unexpectedly invokes preparation.</summary>
        public Task<OperationResult<PreparedDocumentBatch>> PrepareAsync(
            PrepareDocumentsRequest request,
            CancellationToken cancellationToken)
        {
            #region implementation

            return Task.FromResult(OperationResult<PreparedDocumentBatch>.Failure(
                new PreparedDocumentBatch(),
                [
                    new OperationMessage
                    {
                        Code = "test.unexpected-preparation",
                        Message = "No documents were prepared by the navigation stub.",
                        Severity = OperationMessageSeverity.Error
                    }
                ]));

            #endregion
        }

        #endregion
    }

    #endregion
}
