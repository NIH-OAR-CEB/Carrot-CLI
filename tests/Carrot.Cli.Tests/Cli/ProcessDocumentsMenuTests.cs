using Carrot.Cli.Cli.UI;
using Carrot.Cli.CarrotApi;
using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Carrot.Cli.Extraction;
using Carrot.Cli.Input;
using Carrot.Cli.Processing;
using Microsoft.Extensions.Options;
using Spectre.Console.Testing;
using Xunit;

namespace Carrot.Cli.Tests.Cli;

/**************************************************************/
/// <summary>Verifies quoted path entry, retained batch actions, and prepared-results paging.</summary>
public sealed class ProcessDocumentsMenuTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Verifies a quoted path is normalized and the pending process action retains the prepared batch.</summary>
    [Fact]
    public async Task RunAsync_QuotedPath_PreparesReviewsAndOffersPendingProcess()
    {
        #region implementation

        // Arrange
        var root = createTemporaryDirectory();
        try
        {
            var inputPath = Path.Combine(root, "input document.txt");
            await File.WriteAllTextAsync(inputPath, "Source content", TestContext.Current.CancellationToken);
            using var console = createConsole();
            var workflow = new CapturingPreparationWorkflow(createReadyResult(inputPath, 1));
            var menu = createMenu(console, workflow);

            console.Input.PushKey(ConsoleKey.Enter);
            console.Input.PushTextWithEnter($"\"{inputPath}\"");
            pushDownKeys(console, 2);
            console.Input.PushKey(ConsoleKey.Enter);
            console.Input.PushKey(ConsoleKey.Escape);
            pushDownKeys(console, 1);
            console.Input.PushKey(ConsoleKey.Enter);
            console.Input.PushKey(ConsoleKey.Escape);
            pushDownKeys(console, 2);
            console.Input.PushKey(ConsoleKey.Enter);
            pushDownKeys(console, 5);
            console.Input.PushKey(ConsoleKey.Enter);
            console.Input.PushTextWithEnter("y");

            // Act
            await menu.RunAsync(TestContext.Current.CancellationToken);

            // Assert
            var request = Assert.Single(workflow.Requests);
            Assert.Equal(Path.GetFullPath(inputPath), Assert.Single(request.InputPaths));
            Assert.Contains("Prepared Results", console.Output, StringComparison.Ordinal);
            Assert.Contains("Preview JSON Package", console.Output, StringComparison.Ordinal);
            Assert.Contains("\"language\": \"English\"", console.Output, StringComparison.Ordinal);
            Assert.Contains("\"content\": \"Preview content 1\"", console.Output, StringComparison.Ordinal);
            Assert.Contains("no server request was sent", console.Output, StringComparison.Ordinal);
            Assert.Contains("Process Prepared Items", console.Output, StringComparison.Ordinal);
            Assert.Contains("1 prepared document(s) are ready", console.Output, StringComparison.Ordinal);
            Assert.Contains("pending implementation", console.Output, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies page navigation renders every row and Escape returns without canceling the session token.</summary>
    [Fact]
    public async Task ShowAsync_MoreRowsThanPageSize_NavigatesAndEscapesToBatchActions()
    {
        #region implementation

        // Arrange
        using var console = createConsole();
        var pager = new PreparedResultsPager(
            console,
            Options.Create(new CarrotCliOptions { PreparedResultsPageSize = 5 }));
        var result = createReadyResult("C:\\Inputs\\document.txt", 6);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Escape);

        // Act
        await pager.ShowAsync(result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("Page 1/2", console.Output, StringComparison.Ordinal);
        Assert.Contains("Page 2/2", console.Output, StringComparison.Ordinal);
        Assert.Contains("document-6.txt", console.Output, StringComparison.Ordinal);
        Assert.Contains("Press Escape", console.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies Enter continues forward when both Next Page and Previous Page are available.</summary>
    [Fact]
    public async Task ShowAsync_MiddlePage_UsesNextPageAsDefaultChoice()
    {
        #region implementation

        // Arrange
        using var console = createConsole();
        var pager = new PreparedResultsPager(
            console,
            Options.Create(new CarrotCliOptions { PreparedResultsPageSize = 5 }));
        var result = createReadyResult("C:\\Inputs\\document.txt", 11);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Escape);

        // Act
        await pager.ShowAsync(result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("Page 2/3", console.Output, StringComparison.Ordinal);
        Assert.Contains("Page 3/3", console.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies indented JSON and long values are wrapped into navigable pages.</summary>
    [Fact]
    public async Task ShowAsync_LongJsonPackage_PrettyPrintsWrapsAndPages()
    {
        #region implementation

        // Arrange
        using var console = createConsole();
        console.Profile.Width = 50;
        console.Profile.Height = 17;
        var options = Options.Create(new CarrotCliOptions());
        var pager = new PreparedJsonPackagePager(console, new ClusterRequestFactory(options));
        var result = createReadyResult("C:\\Inputs\\document.txt", 1, contentLength: 220);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Escape);

        // Act
        await pager.ShowAsync(result.Value!, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("  \"language\": \"English\"", console.Output, StringComparison.Ordinal);
        Assert.Contains("Page 1/", console.Output, StringComparison.Ordinal);
        Assert.Contains("Page 2/", console.Output, StringComparison.Ordinal);
        Assert.Contains("↪", console.Output, StringComparison.Ordinal);
        Assert.Contains("Press Escape to return to Batch Actions", console.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates Process Documents with real prompt validation and a controlled preparation boundary.</summary>
    private static ProcessDocumentsMenu createMenu(
        TestConsole console,
        IDocumentPreparationWorkflow workflow)
    {
        #region implementation

        var options = Options.Create(new CarrotCliOptions());
        var formats = new DocumentFormatCatalog();
        var resolver = new InputSourceResolver(
            new FolderInputSourceLoader(formats, options),
            new FileInputSourceLoader(formats, options),
            new ZipInputSourceLoader(formats, options),
            formats);
        var markdownRenderer = new MarkdownHelpRenderer(console);
        var helpRenderer = new HelpRenderer(
            console,
            new HelpTopicCatalog(),
            new EmbeddedHelpContentProvider(),
            markdownRenderer);
        return new ProcessDocumentsMenu(
            console,
            helpRenderer,
            new InputPathNormalizer(),
            resolver,
            workflow,
            new PreparedResultsPager(console, options),
            new PreparedJsonPackagePager(console, new ClusterRequestFactory(options)));

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a successful prepared batch with deterministic review rows and extracted documents.</summary>
    private static OperationResult<PreparedDocumentBatch> createReadyResult(
        string sourcePath,
        int count,
        int contentLength = 0)
    {
        #region implementation

        var containerPath = Path.GetDirectoryName(sourcePath) ?? "C:\\Inputs";
        var rows = new List<PreparedDocumentRow>();
        var documents = new List<ExtractedDocument>();
        for (var index = 0; index < count; index++)
        {
            var fileName = count == 1 ? Path.GetFileName(sourcePath) : $"document-{index + 1}.txt";
            var physicalPath = count == 1 ? sourcePath : Path.Combine(containerPath, fileName);
            var content = contentLength > 0
                ? new string((char)('a' + (index % 26)), contentLength)
                : $"Preview content {index + 1}";
            var sourceFile = new SourceFile
            {
                SourceOrdinal = index,
                SourceKey = physicalPath,
                ContainerPath = containerPath,
                PhysicalPath = physicalPath,
                RelativePath = fileName,
                FileName = fileName,
                Extension = ".txt",
                SizeBytes = 20 + index
            };
            documents.Add(new ExtractedDocument
            {
                SourceFile = sourceFile,
                CarrotDocumentIndex = index,
                Title = Path.GetFileNameWithoutExtension(fileName),
                Content = content,
                Sha256 = new string('A', 64)
            });
            rows.Add(new PreparedDocumentRow
            {
                SourceOrdinal = index,
                CarrotDocumentIndex = index,
                ContainerPath = containerPath,
                RelativePath = fileName,
                FileName = fileName,
                Extension = ".txt",
                SizeBytes = 20 + index,
                Sha256 = new string('A', 64),
                Status = PreparedDocumentStatus.Ready,
                ExtractedCharacterCount = content.Length,
                ContentPreview = content
            });
        }

        return OperationResult<PreparedDocumentBatch>.Success(new PreparedDocumentBatch
        {
            Rows = rows.AsReadOnly(),
            Documents = documents.AsReadOnly()
        });

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates an interactive test console.</summary>
    private static TestConsole createConsole()
    {
        #region implementation

        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        return console;

        #endregion
    }

    /**************************************************************/
    /// <summary>Queues a deterministic number of down-arrow inputs.</summary>
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
    /// <summary>Creates a uniquely named temporary directory.</summary>
    private static string createTemporaryDirectory()
    {
        #region implementation

        var path = Path.Combine(Path.GetTempPath(), $"carrot-cli-menu-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;

        #endregion
    }

    /**************************************************************/
    /// <summary>Deletes an owned test directory after resolving its exact absolute path.</summary>
    private static void deleteTemporaryDirectory(string path)
    {
        #region implementation

        var fullPath = Path.GetFullPath(path);
        if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, recursive: true);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Captures normalized preparation requests and returns one controlled result.</summary>
    private sealed class CapturingPreparationWorkflow : IDocumentPreparationWorkflow
    {
        #region implementation

        private readonly OperationResult<PreparedDocumentBatch> _result;

        /**************************************************************/
        /// <summary>Initializes the fake with its deterministic preparation result.</summary>
        internal CapturingPreparationWorkflow(OperationResult<PreparedDocumentBatch> result)
        {
            #region implementation

            _result = result;

            #endregion
        }

        /**************************************************************/
        /// <summary>Gets every request received by the fake in invocation order.</summary>
        internal List<PrepareDocumentsRequest> Requests { get; } = [];

        /**************************************************************/
        /// <summary>Captures the request and returns the configured result.</summary>
        public Task<OperationResult<PreparedDocumentBatch>> PrepareAsync(
            PrepareDocumentsRequest request,
            CancellationToken cancellationToken)
        {
            #region implementation

            Requests.Add(request);
            return Task.FromResult(_result);

            #endregion
        }

        #endregion
    }

    #endregion
}
