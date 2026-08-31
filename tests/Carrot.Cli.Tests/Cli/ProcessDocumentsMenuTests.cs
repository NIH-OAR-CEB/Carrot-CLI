using Carrot.Cli.Cli.UI;
using Carrot.Cli.CarrotApi;
using Carrot.Cli.CarrotApi.Contracts;
using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Carrot.Cli.Extraction;
using Carrot.Cli.Input;
using Carrot.Cli.Processing;
using Carrot.Cli.Reporting;
using Microsoft.Extensions.Options;
using Spectre.Console.Testing;
using Xunit;

namespace Carrot.Cli.Tests.Cli;

/**************************************************************/
/// <summary>Verifies endpoint prompting, processing state retention, paging defaults, Escape, and zero-ready behavior.</summary>
public sealed class ProcessDocumentsMenuTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Verifies processing uses the default endpoint, opens results automatically, and retains a view action.</summary>
    [Fact]
    public async Task RunAsync_ProcessSuccess_UsesDefaultEndpointAndOpensProcessedResults()
    {
        #region implementation

        // Arrange
        var root = createTemporaryDirectory();
        try
        {
            var inputPath = Path.Combine(root, "input document.txt");
            await File.WriteAllTextAsync(inputPath, "Source content", TestContext.Current.CancellationToken);
            using var console = createConsole();
            var preparation = new CapturingPreparationWorkflow(createReadyResult(inputPath, 1));
            var processor = new QueuedPreparedDocumentProcessor();
            processor.EnqueueSuccess();
            var exporter = new CapturingProcessedResultsExporter();
            var menu = createMenu(console, preparation, processor, exporter);
            queuePreparation(console, inputPath);
            console.Input.PushKey(ConsoleKey.Escape); // Leave automatic prepared results.
            pushDownKeys(console, 2);
            console.Input.PushKey(ConsoleKey.Enter); // Process Prepared Items.
            console.Input.PushKey(ConsoleKey.Enter); // Accept the default endpoint.
            console.Input.PushKey(ConsoleKey.Escape); // Leave automatic processed results.
            pushDownKeys(console, 1);
            console.Input.PushKey(ConsoleKey.Enter); // View Processed Results.
            console.Input.PushKey(ConsoleKey.Escape);
            pushDownKeys(console, 2);
            console.Input.PushKey(ConsoleKey.Enter); // Save Processed Results to Excel.
            var outputPath = Path.Combine(root, "processed results.xlsx");
            console.Input.PushTextWithEnter($"\"{outputPath}\"");
            pushDownKeys(console, 7);
            console.Input.PushKey(ConsoleKey.Enter); // Back to Main Menu.
            console.Input.PushTextWithEnter("y");

            // Act
            await menu.RunAsync(TestContext.Current.CancellationToken);

            // Assert
            var preparationRequest = Assert.Single(preparation.Requests);
            Assert.Equal(Path.GetFullPath(inputPath), Assert.Single(preparationRequest.InputPaths));
            var processingRequest = Assert.Single(processor.Requests);
            Assert.Equal("http://localhost:8080/service", processingRequest.Endpoint.AbsoluteUri.TrimEnd('/'));
            Assert.Same(preparation.Result.Value, processingRequest.PreparedBatch);
            Assert.Contains("complete extracted text will be sent", console.Output, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Processing: Success", console.Output, StringComparison.Ordinal);
            Assert.Contains("View Processed Results", console.Output, StringComparison.Ordinal);
            Assert.Contains("Save Processed Results to Excel", console.Output, StringComparison.Ordinal);
            Assert.True(countOccurrences(console.Output, "Processed Results") >= 2);
            Assert.DoesNotContain("No files were written", console.Output, StringComparison.Ordinal);
            var exportRequest = Assert.Single(exporter.Requests);
            Assert.Same(processor.SuccessfulBatches[0], exportRequest.Batch);
            Assert.Equal(outputPath, exportRequest.OutputPath);
            Assert.False(exportRequest.Overwrite);
            Assert.Contains("Excel report saved", console.Output, StringComparison.Ordinal);
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies invalid endpoint input is rejected and a valid trailing-slash endpoint is normalized.</summary>
    [Fact]
    public async Task RunAsync_InvalidThenValidEndpoint_RepromptsAndNormalizes()
    {
        #region implementation

        // Arrange
        var root = createTemporaryDirectory();
        try
        {
            var inputPath = Path.Combine(root, "input.txt");
            await File.WriteAllTextAsync(inputPath, "Source", TestContext.Current.CancellationToken);
            using var console = createConsole();
            var preparation = new CapturingPreparationWorkflow(createReadyResult(inputPath, 1));
            var processor = new QueuedPreparedDocumentProcessor();
            processor.EnqueueSuccess();
            var menu = createMenu(console, preparation, processor);
            queuePreparation(console, inputPath);
            console.Input.PushKey(ConsoleKey.Escape);
            pushDownKeys(console, 2);
            console.Input.PushKey(ConsoleKey.Enter);
            console.Input.PushTextWithEnter("https://carrot.example/not-service");
            console.Input.PushTextWithEnter("https://carrot.example/service/");
            console.Input.PushKey(ConsoleKey.Escape);
            pushDownKeys(console, 7);
            console.Input.PushKey(ConsoleKey.Enter);
            console.Input.PushTextWithEnter("y");

            // Act
            await menu.RunAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Contains("endpoint path must end in", console.Output, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(
                "https://carrot.example/service",
                Assert.Single(processor.Requests).Endpoint.AbsoluteUri.TrimEnd('/'));
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies a failed reprocess leaves the previous successful result available.</summary>
    [Fact]
    public async Task RunAsync_FailedReprocess_RetainsPreviousSuccessfulResult()
    {
        #region implementation

        // Arrange
        var root = createTemporaryDirectory();
        try
        {
            var inputPath = Path.Combine(root, "input.txt");
            await File.WriteAllTextAsync(inputPath, "Source", TestContext.Current.CancellationToken);
            using var console = createConsole();
            var preparation = new CapturingPreparationWorkflow(createReadyResult(inputPath, 1));
            var processor = new QueuedPreparedDocumentProcessor();
            processor.EnqueueSuccess();
            processor.EnqueueFailure("Carrot is temporarily unavailable.");
            var exporter = new CapturingProcessedResultsExporter();
            var menu = createMenu(console, preparation, processor, exporter);
            queuePreparation(console, inputPath);
            console.Input.PushKey(ConsoleKey.Escape);
            pushDownKeys(console, 2);
            console.Input.PushKey(ConsoleKey.Enter); // First process.
            console.Input.PushKey(ConsoleKey.Enter);
            console.Input.PushKey(ConsoleKey.Escape);
            pushDownKeys(console, 4);
            console.Input.PushKey(ConsoleKey.Enter); // Reprocess.
            console.Input.PushKey(ConsoleKey.Enter);
            pushDownKeys(console, 1);
            console.Input.PushKey(ConsoleKey.Enter); // View retained processed result.
            console.Input.PushKey(ConsoleKey.Escape);
            pushDownKeys(console, 2);
            console.Input.PushKey(ConsoleKey.Enter); // Export retained processed result.
            console.Input.PushTextWithEnter(Path.Combine(root, "retained.xlsx"));
            pushDownKeys(console, 7);
            console.Input.PushKey(ConsoleKey.Enter);
            console.Input.PushTextWithEnter("y");

            // Act
            await menu.RunAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(2, processor.Requests.Count);
            Assert.Contains("Processing: Failure", console.Output, StringComparison.Ordinal);
            Assert.Contains("Carrot is temporarily unavailable", console.Output, StringComparison.Ordinal);
            Assert.Contains("previous successful processed result remains available", console.Output, StringComparison.OrdinalIgnoreCase);
            Assert.True(countOccurrences(console.Output, "Processed Results") >= 2);
            Assert.Same(processor.SuccessfulBatches[0], Assert.Single(exporter.Requests).Batch);
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies zero-ready batches never prompt for an endpoint or invoke processing.</summary>
    [Fact]
    public async Task RunAsync_ZeroReadyItems_ReportsUnavailableWithoutProcessing()
    {
        #region implementation

        // Arrange
        var root = createTemporaryDirectory();
        try
        {
            var inputPath = Path.Combine(root, "input.txt");
            await File.WriteAllTextAsync(inputPath, "Source", TestContext.Current.CancellationToken);
            using var console = createConsole();
            var emptyBatch = new PreparedDocumentBatch();
            var preparationResult = OperationResult<PreparedDocumentBatch>.Failure(
                emptyBatch,
                [new OperationMessage
                {
                    Code = "preparation.no-ready-documents",
                    Message = "No documents were successfully prepared.",
                    Severity = OperationMessageSeverity.Error
                }]);
            var processor = new QueuedPreparedDocumentProcessor();
            var menu = createMenu(
                console,
                new CapturingPreparationWorkflow(preparationResult),
                processor);
            queuePreparation(console, inputPath);
            console.Input.PushKey(ConsoleKey.Escape);
            pushDownKeys(console, 2);
            console.Input.PushKey(ConsoleKey.Enter); // Process unavailable.
            pushDownKeys(console, 5);
            console.Input.PushKey(ConsoleKey.Enter);
            console.Input.PushTextWithEnter("y");

            // Act
            await menu.RunAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Empty(processor.Requests);
            Assert.Contains("Process Prepared Items (unavailable", console.Output, StringComparison.Ordinal);
            Assert.Contains("No successfully prepared documents are available to process", console.Output, StringComparison.Ordinal);
            Assert.DoesNotContain("Carrot service endpoint", console.Output, StringComparison.Ordinal);
            Assert.DoesNotContain("Save Processed Results to Excel", console.Output, StringComparison.Ordinal);
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies prepared page navigation renders every row and Escape returns to batch actions.</summary>
    [Fact]
    public async Task PreparedPager_MoreRowsThanPageSize_NavigatesAndEscapes()
    {
        #region implementation

        // Arrange
        using var console = createConsole();
        var pager = new PreparedResultsPager(console, Options.Create(new CarrotCliOptions()));
        var result = createReadyResult("C:\\Inputs\\document.txt", 6);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Escape);

        // Act
        await pager.ShowAsync(result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("Page 1/2", console.Output, StringComparison.Ordinal);
        Assert.Contains("Page 2/2", console.Output, StringComparison.Ordinal);
        Assert.Contains("document-6.txt", console.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies processed paging uses Next Page as the default on every forward page.</summary>
    [Fact]
    public async Task ProcessedPager_MiddlePage_UsesNextPageAsDefaultAndPreservesScores()
    {
        #region implementation

        // Arrange
        using var console = createConsole();
        console.Profile.Width = 200;
        var prepared = createReadyResult("C:\\Inputs\\document.txt", 11).Value!;
        var processed = createProcessedBatch(
            new ProcessPreparedItemsRequest
            {
                Endpoint = new Uri("http://localhost:8080/service"),
                PreparedBatch = prepared,
                Timeout = TimeSpan.FromSeconds(120)
            });
        var pager = new ProcessedResultsPager(
            console,
            Options.Create(new CarrotCliOptions()),
            createExportFlow(console));
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Escape);

        // Act
        await pager.ShowAsync(processed, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("Page 1/3", console.Output, StringComparison.Ordinal);
        Assert.Contains("Page 2/3", console.Output, StringComparison.Ordinal);
        Assert.Contains("Page 3/3", console.Output, StringComparison.Ordinal);
        Assert.Contains("document-11.txt", console.Output, StringComparison.Ordinal);
        Assert.Contains("0.12345678901234566", console.Output, StringComparison.Ordinal);
        Assert.Contains("Scores are relative only within this response", console.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies the final processed page defaults to Excel export and returns to the same page after saving.
    /// </summary>
    [Fact]
    public async Task ProcessedPager_FinalPage_DefaultsToSaveAndRetainsCurrentPage()
    {
        #region implementation

        // Arrange
        using var console = createConsole();
        console.Profile.Width = 200;
        var prepared = createReadyResult("C:\\Inputs\\document.txt", 1).Value!;
        var processed = createProcessedBatch(
            new ProcessPreparedItemsRequest
            {
                Endpoint = new Uri("http://localhost:8080/service"),
                PreparedBatch = prepared,
                Timeout = TimeSpan.FromSeconds(120)
            });
        var exporter = new CapturingProcessedResultsExporter();
        var pager = new ProcessedResultsPager(
            console,
            Options.Create(new CarrotCliOptions()),
            createExportFlow(console, exporter));
        console.Input.PushKey(ConsoleKey.Enter); // Save Processed Results to Excel.
        console.Input.PushKey(ConsoleKey.Enter); // Accept the suggested destination.
        console.Input.PushKey(ConsoleKey.Escape); // Return to Batch Actions after the page is redisplayed.

        // Act
        await pager.ShowAsync(processed, TestContext.Current.CancellationToken);

        // Assert
        var request = Assert.Single(exporter.Requests);
        Assert.Same(processed, request.Batch);
        Assert.Contains("Save Processed Results to Excel", console.Output, StringComparison.Ordinal);
        Assert.Contains("Excel report saved", console.Output, StringComparison.Ordinal);
        Assert.Equal(2, countOccurrences(console.Output, "Page 1/1"));

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies indented JSON and long values remain local and are wrapped into navigable pages.</summary>
    [Fact]
    public async Task JsonPager_LongPackage_PrettyPrintsWrapsAndPages()
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
        Assert.Contains("no server request was sent", console.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>Queues path addition and preparation through the interactive setup prompts.</summary>
    /// <param name="console">The test console.</param>
    /// <param name="inputPath">The valid direct-file input path.</param>
    private static void queuePreparation(TestConsole console, string inputPath)
    {
        #region implementation

        console.Input.PushKey(ConsoleKey.Enter); // Add Path.
        console.Input.PushTextWithEnter($"\"{inputPath}\"");
        pushDownKeys(console, 2);
        console.Input.PushKey(ConsoleKey.Enter); // Prepare Documents.

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates Process Documents with real prompt validation and controlled workflow boundaries.</summary>
    /// <param name="console">The interactive test console.</param>
    /// <param name="workflow">The preparation boundary.</param>
    /// <param name="processor">The prepared-item processing boundary.</param>
    /// <returns>The fully wired menu.</returns>
    private static ProcessDocumentsMenu createMenu(
        TestConsole console,
        IDocumentPreparationWorkflow workflow,
        IPreparedDocumentProcessor processor,
        IProcessedResultsExporter? exporter = null)
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
        var exportFlow = createExportFlow(console, exporter);
        return new ProcessDocumentsMenu(
            console,
            helpRenderer,
            new InputPathNormalizer(),
            resolver,
            workflow,
            new PreparedResultsPager(console, options),
            new PreparedJsonPackagePager(console, new ClusterRequestFactory(options)),
            new EndpointResolver(),
            processor,
            new ProcessedResultsPager(console, options, exportFlow),
            exportFlow,
            options);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates the real Excel prompt flow with an optional capturing persistence boundary.</summary>
    /// <param name="console">The console receiving the export interaction.</param>
    /// <param name="exporter">The optional exporter used to capture save requests.</param>
    /// <returns>The fully wired retained-result export flow.</returns>
    private static ProcessedResultsExportFlow createExportFlow(
        TestConsole console,
        IProcessedResultsExporter? exporter = null)
    {
        #region implementation

        return new ProcessedResultsExportFlow(
            console,
            new ExcelOutputPathResolver(),
            new ExcelOutputPathSuggester(TimeProvider.System),
            exporter ?? new CapturingProcessedResultsExporter());

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a successful prepared batch with deterministic rows and documents.</summary>
    /// <param name="sourcePath">The representative physical source path.</param>
    /// <param name="count">The ready document count.</param>
    /// <param name="contentLength">The optional fixed content length.</param>
    /// <returns>The successful preparation result.</returns>
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
    /// <summary>Creates an assigned processed batch directly from one captured processing request.</summary>
    /// <param name="request">The captured processing request.</param>
    /// <returns>The complete successful processed batch.</returns>
    private static ProcessedDocumentBatch createProcessedBatch(ProcessPreparedItemsRequest request)
    {
        #region implementation

        var clusterRequest = new ClusterRequestFactory(Options.Create(new CarrotCliOptions()))
            .Create(request.PreparedBatch.Documents);
        var rows = request.PreparedBatch.Documents
            .Select((document, index) => new ProcessedDocumentRow
            {
                CarrotDocumentIndex = index,
                PreparedDocument = document,
                Memberships =
                [
                    new ClusterMembership
                    {
                        CarrotDocumentIndex = index,
                        Labels = ["Category"],
                        CategoryPath = $"Category > Item {index + 1}",
                        Score = 0.12345678901234566D,
                        Depth = 1
                    }
                ]
            })
            .ToArray();
        return new ProcessedDocumentBatch
        {
            RunId = Guid.Parse("11111111-2222-3333-4444-555555555555"),
            Endpoint = request.Endpoint,
            Request = clusterRequest,
            Response = new ClusterResponse(),
            Rows = Array.AsReadOnly(rows)
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates an interactive test console.</summary>
    /// <returns>The configured console.</returns>
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
    /// <param name="console">The console input queue.</param>
    /// <param name="count">The number of down-arrow keys.</param>
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
    /// <summary>Counts exact substring occurrences in captured console output.</summary>
    /// <param name="text">The complete output.</param>
    /// <param name="value">The substring to count.</param>
    /// <returns>The nonoverlapping occurrence count.</returns>
    private static int countOccurrences(string text, string value)
    {
        #region implementation

        var count = 0;
        var position = 0;
        while ((position = text.IndexOf(value, position, StringComparison.Ordinal)) >= 0)
        {
            count++;
            position += value.Length;
        }

        return count;

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a uniquely named temporary directory.</summary>
    /// <returns>The owned directory path.</returns>
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
    /// <param name="path">The exact owned test path.</param>
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

        /**************************************************************/
        /// <summary>Initializes the fake with its deterministic preparation result.</summary>
        /// <param name="result">The result returned for every call.</param>
        internal CapturingPreparationWorkflow(OperationResult<PreparedDocumentBatch> result)
        {
            #region implementation

            Result = result;

            #endregion
        }

        /**************************************************************/
        /// <summary>Gets the controlled preparation result.</summary>
        internal OperationResult<PreparedDocumentBatch> Result { get; }

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
            return Task.FromResult(Result);

            #endregion
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Returns queued success or failure outcomes while capturing every processing request.</summary>
    private sealed class QueuedPreparedDocumentProcessor : IPreparedDocumentProcessor
    {
        #region implementation

        private readonly Queue<Func<ProcessPreparedItemsRequest, OperationResult<ProcessedDocumentBatch>>> _results = [];

        /**************************************************************/
        /// <summary>Gets every processing request in invocation order.</summary>
        internal List<ProcessPreparedItemsRequest> Requests { get; } = [];

        /**************************************************************/
        /// <summary>Gets every successful processed batch returned by the fake.</summary>
        internal List<ProcessedDocumentBatch> SuccessfulBatches { get; } = [];

        /**************************************************************/
        /// <summary>Queues one success derived from the actual retained batch.</summary>
        internal void EnqueueSuccess()
        {
            #region implementation

            _results.Enqueue(request =>
            {
                var batch = createProcessedBatch(request);
                SuccessfulBatches.Add(batch);
                return OperationResult<ProcessedDocumentBatch>.Success(batch);
            });

            #endregion
        }

        /**************************************************************/
        /// <summary>Queues one structured processing failure.</summary>
        /// <param name="message">The safe failure message.</param>
        internal void EnqueueFailure(string message)
        {
            #region implementation

            _results.Enqueue(_ => OperationResult<ProcessedDocumentBatch>.Failure(
                [new OperationMessage
                {
                    Code = "test.processing",
                    Message = message,
                    Severity = OperationMessageSeverity.Error
                }]));

            #endregion
        }

        /**************************************************************/
        /// <summary>Captures one request and returns the next queued result.</summary>
        public Task<OperationResult<ProcessedDocumentBatch>> ProcessAsync(
            ProcessPreparedItemsRequest request,
            CancellationToken cancellationToken)
        {
            #region implementation

            Requests.Add(request);
            if (_results.Count == 0)
            {
                throw new InvalidOperationException("No processing result was queued for this test.");
            }

            return Task.FromResult(_results.Dequeue()(request));

            #endregion
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Captures explicit Excel export requests and reports their normalized paths as saved.</summary>
    private sealed class CapturingProcessedResultsExporter : IProcessedResultsExporter
    {
        #region implementation

        /**************************************************************/
        /// <summary>Gets every export request in invocation order.</summary>
        internal List<SaveProcessedResultsRequest> Requests { get; } = [];

        /**************************************************************/
        /// <summary>Captures one request and returns its path as a successful save.</summary>
        public Task<OperationResult<string>> SaveAsync(
            SaveProcessedResultsRequest request,
            CancellationToken cancellationToken)
        {
            #region implementation

            Requests.Add(request);
            return Task.FromResult(OperationResult<string>.Success(request.OutputPath));

            #endregion
        }

        #endregion
    }

    #endregion
}
