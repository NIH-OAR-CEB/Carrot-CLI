using Carrot.Cli.Cli.UI;
using Carrot.Cli.Common;
using Carrot.Cli.Reporting;
using Carrot.Cli.Tests.Reporting;
using Spectre.Console.Testing;
using Xunit;

namespace Carrot.Cli.Tests.Cli;

/**************************************************************/
/// <summary>Verifies interactive Excel path validation, overwrite decisions, and retained feedback.</summary>
public sealed class ProcessedResultsExportFlowTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Verifies invalid extension input reprompts before one normalized save succeeds.</summary>
    [Fact]
    public async Task RunAsync_InvalidThenValidPath_RepromptsAndSavesOnce()
    {
        #region implementation

        var root = createTemporaryDirectory();
        try
        {
            // Arrange
            using var console = createConsole();
            var exporter = new CapturingExporter();
            var flow = new ProcessedResultsExportFlow(
                console,
                new ExcelOutputPathResolver(),
                new ExcelOutputPathSuggester(TimeProvider.System),
                exporter);
            var invalidPath = Path.Combine(root, "result.csv");
            var outputPath = Path.Combine(root, "processed result.xlsx");
            console.Input.PushTextWithEnter(invalidPath);
            console.Input.PushTextWithEnter($"\"{outputPath}\"");

            // Act
            await flow.RunAsync(ReportingTestData.CreateProcessedBatch(), TestContext.Current.CancellationToken);

            // Assert
            Assert.Contains("must end in .xlsx", console.Output, StringComparison.OrdinalIgnoreCase);
            var request = Assert.Single(exporter.Requests);
            Assert.Equal(outputPath, request.OutputPath);
            Assert.False(request.Overwrite);
            Assert.Contains("Excel report saved", console.Output, StringComparison.Ordinal);
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies Enter accepts the complete suggested path without requiring any pathname typing.</summary>
    [Fact]
    public async Task RunAsync_DefaultPathAccepted_SavesSuggestedWorkbook()
    {
        #region implementation

        // Arrange
        using var console = createConsole();
        var exporter = new CapturingExporter();
        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(2099, 12, 31, 23, 59, 58, TimeSpan.Zero));
        var pathSuggester = new ExcelOutputPathSuggester(timeProvider);
        var expectedPath = pathSuggester.Suggest();
        var flow = new ProcessedResultsExportFlow(
            console,
            new ExcelOutputPathResolver(),
            pathSuggester,
            exporter);
        console.Input.PushKey(ConsoleKey.Enter);

        // Act
        await flow.RunAsync(ReportingTestData.CreateProcessedBatch(), TestContext.Current.CancellationToken);

        // Assert
        var request = Assert.Single(exporter.Requests);
        Assert.Equal(expectedPath, request.OutputPath);
        Assert.EndsWith("carrot-results-20991231-235958.xlsx", request.OutputPath, StringComparison.Ordinal);
        Assert.Contains("press Enter to accept", console.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Excel report saved", console.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies declining overwrite leaves the existing file untouched and skips the exporter.</summary>
    [Fact]
    public async Task RunAsync_ExistingFileOverwriteDeclined_DoesNotExport()
    {
        #region implementation

        var root = createTemporaryDirectory();
        try
        {
            // Arrange
            var outputPath = Path.Combine(root, "existing.xlsx");
            await File.WriteAllTextAsync(outputPath, "original", TestContext.Current.CancellationToken);
            using var console = createConsole();
            var exporter = new CapturingExporter();
            var flow = new ProcessedResultsExportFlow(
                console,
                new ExcelOutputPathResolver(),
                new ExcelOutputPathSuggester(TimeProvider.System),
                exporter);
            console.Input.PushTextWithEnter(outputPath);
            console.Input.PushTextWithEnter("n");

            // Act
            await flow.RunAsync(ReportingTestData.CreateProcessedBatch(), TestContext.Current.CancellationToken);

            // Assert
            Assert.Empty(exporter.Requests);
            Assert.Equal("original", await File.ReadAllTextAsync(outputPath, TestContext.Current.CancellationToken));
            Assert.Contains("existing workbook was not changed", console.Output, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies approved overwrite forwards replacement permission and a failure remains operator-visible.</summary>
    [Fact]
    public async Task RunAsync_ExistingFileOverwriteApprovedAndWriterFails_ShowsSafeFailure()
    {
        #region implementation

        var root = createTemporaryDirectory();
        try
        {
            // Arrange
            var outputPath = Path.Combine(root, "existing.xlsx");
            await File.WriteAllTextAsync(outputPath, "original", TestContext.Current.CancellationToken);
            using var console = createConsole();
            var exporter = new CapturingExporter
            {
                Result = OperationResult<string>.Failure(
                    [new OperationMessage
                    {
                        Code = "report.write.failed",
                        Message = "The workbook is locked.",
                        Severity = OperationMessageSeverity.Error
                    }])
            };
            var flow = new ProcessedResultsExportFlow(
                console,
                new ExcelOutputPathResolver(),
                new ExcelOutputPathSuggester(TimeProvider.System),
                exporter);
            console.Input.PushTextWithEnter(outputPath);
            console.Input.PushTextWithEnter("y");

            // Act
            await flow.RunAsync(ReportingTestData.CreateProcessedBatch(), TestContext.Current.CancellationToken);

            // Assert
            Assert.True(Assert.Single(exporter.Requests).Overwrite);
            Assert.Contains("Excel export failed", console.Output, StringComparison.Ordinal);
            Assert.Contains("workbook is locked", console.Output, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

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
    /// <summary>Creates an exact owned export-flow test directory.</summary>
    /// <returns>The absolute directory.</returns>
    private static string createTemporaryDirectory()
    {
        #region implementation

        var path = Path.Combine(Path.GetTempPath(), $"carrot-export-flow-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;

        #endregion
    }

    /**************************************************************/
    /// <summary>Deletes one exact owned export-flow directory.</summary>
    /// <param name="path">The owned directory.</param>
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
    /// <summary>Captures export requests and returns a controlled operation result.</summary>
    private sealed class CapturingExporter : IProcessedResultsExporter
    {
        #region implementation

        /**************************************************************/
        /// <summary>Gets the controlled result; defaults to returning the request path successfully.</summary>
        internal OperationResult<string>? Result { get; init; }

        /**************************************************************/
        /// <summary>Gets captured export requests.</summary>
        internal List<SaveProcessedResultsRequest> Requests { get; } = [];

        /**************************************************************/
        /// <summary>Captures one request and returns the controlled or default successful result.</summary>
        public Task<OperationResult<string>> SaveAsync(
            SaveProcessedResultsRequest request,
            CancellationToken cancellationToken)
        {
            #region implementation

            Requests.Add(request);
            return Task.FromResult(Result ?? OperationResult<string>.Success(request.OutputPath));

            #endregion
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Supplies a deterministic UTC instant and UTC local zone for filename assertions.</summary>
    private sealed class FixedTimeProvider : TimeProvider
    {
        #region implementation

        private readonly DateTimeOffset _utcNow;

        /**************************************************************/
        /// <summary>Initializes the provider with one fixed UTC instant.</summary>
        /// <param name="utcNow">The instant returned by <see cref="GetUtcNow"/>.</param>
        internal FixedTimeProvider(DateTimeOffset utcNow)
        {
            #region implementation

            _utcNow = utcNow.ToUniversalTime();

            #endregion
        }

        /**************************************************************/
        /// <summary>Gets UTC as the deterministic local time zone.</summary>
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        /**************************************************************/
        /// <summary>Returns the fixed UTC instant.</summary>
        /// <returns>The configured instant.</returns>
        public override DateTimeOffset GetUtcNow()
        {
            #region implementation

            return _utcNow;

            #endregion
        }

        #endregion
    }

    #endregion
}
