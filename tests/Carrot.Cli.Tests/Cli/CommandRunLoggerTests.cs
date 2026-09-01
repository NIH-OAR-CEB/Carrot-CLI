using System.Text;
using Carrot.Cli.Cli.Reporting;
using Carrot.Cli.Common;
using Carrot.Cli.Processing;
using Xunit;

namespace Carrot.Cli.Tests.Cli;

/**************************************************************/
/// <summary>
/// Verifies append-only, safe, and cancellation-aware command-run diagnostics.
/// </summary>
public sealed class CommandRunLoggerTests
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Verifies lifecycle, diagnostics, terminal state, exit code, and artifact paths use deterministic UTF-8 lines.
    /// </summary>
    [Fact]
    public async Task WriteMethods_ValidLogPath_AppendsSafeDeterministicRecords()
    {
        #region implementation

        // Arrange
        var root = createTemporaryDirectory();
        try
        {
            var path = Path.Combine(root, "run.log");
            await File.WriteAllTextAsync(path, "previous record\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), TestContext.Current.CancellationToken);
            var logger = new CommandRunLogger(new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 16, 30, 0, TimeSpan.Zero)));
            var result = new ProcessRunResult
            {
                RunId = Guid.Parse("11223344-5566-7788-99aa-bbccddeeff00"),
                Status = OperationStatus.PartialSuccess,
                ExitCode = 2,
                Messages =
                [
                    new OperationMessage { Code = "run.info", Message = "Started\r\nnext", Severity = OperationMessageSeverity.Information },
                    new OperationMessage { Code = "file.warning", Message = "Skipped", Severity = OperationMessageSeverity.Warning },
                    new OperationMessage { Code = "run.failed", Message = "Failed", Severity = OperationMessageSeverity.Error }
                ],
                ArtifactPaths = ["C:\\reports\\summary.xlsx"]
            };

            // Act
            await logger.WriteStartedAsync(path, "process", TestContext.Current.CancellationToken);
            await logger.WriteResultAsync(path, result, TestContext.Current.CancellationToken);
            await logger.WriteCancellationAsync(path);

            // Assert
            var bytes = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
            Assert.DoesNotContain(Encoding.UTF8.GetPreamble(), bytes);
            Assert.Equal(
                "previous record\n"
                + "2026-09-01T16:30:00.0000000+00:00 Information [run.started] Command started: process.\n"
                + "2026-09-01T16:30:00.0000000+00:00 Information [run.info] Started  next\n"
                + "2026-09-01T16:30:00.0000000+00:00 Warning [file.warning] Skipped\n"
                + "2026-09-01T16:30:00.0000000+00:00 Error [run.failed] Failed\n"
                + "2026-09-01T16:30:00.0000000+00:00 Information [run.completed] Run ID: 11223344-5566-7788-99aa-bbccddeeff00.\n"
                + "2026-09-01T16:30:00.0000000+00:00 Information [run.status] Status: PartialSuccess.\n"
                + "2026-09-01T16:30:00.0000000+00:00 Information [run.exit-code] Exit code: 2.\n"
                + "2026-09-01T16:30:00.0000000+00:00 Information [artifact.path] Path: C:\\reports\\summary.xlsx\n"
                + "2026-09-01T16:30:00.0000000+00:00 Warning [run.cancelled] Command cancelled.\n",
                Encoding.UTF8.GetString(bytes).ReplaceLineEndings("\n"));
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies omitted paths are no-ops and canceled normal writes propagate cancellation before creating a file.
    /// </summary>
    [Fact]
    public async Task WriteMethods_OmittedOrCancelledPath_DoesNotCreateOutputAndPropagatesCancellation()
    {
        #region implementation

        // Arrange
        var root = createTemporaryDirectory();
        try
        {
            var path = Path.Combine(root, "cancelled.log");
            var logger = new CommandRunLogger(TimeProvider.System);
            using var cancellationSource = new CancellationTokenSource();
            cancellationSource.Cancel();

            // Act and assert
            await logger.WriteStartedAsync(null, "process", cancellationSource.Token);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => logger.WriteStartedAsync(path, "process", cancellationSource.Token));
            await logger.WriteCancellationAsync(path);

            Assert.Equal(
                "Command cancelled.",
                Assert.Single(await File.ReadAllLinesAsync(path, TestContext.Current.CancellationToken)).Split(" ", 4)[3]);
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies in-process concurrent callers append complete groups without interleaving individual records.
    /// </summary>
    [Fact]
    public async Task WriteStartedAsync_ConcurrentCalls_AppendsOneCompleteLinePerCall()
    {
        #region implementation

        // Arrange
        var root = createTemporaryDirectory();
        try
        {
            var path = Path.Combine(root, "concurrent.log");
            var logger = new CommandRunLogger(TimeProvider.System);

            // Act
            await Task.WhenAll(Enumerable.Range(0, 40).Select(index => logger.WriteStartedAsync(path, $"process-{index}", TestContext.Current.CancellationToken)));

            // Assert
            var lines = await File.ReadAllLinesAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal(40, lines.Length);
            Assert.All(lines, line => Assert.Matches("^\\d{4}-\\d{2}-\\d{2}T.* Information \\[run\\.started\\] Command started: process-\\d+\\.$", line));
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies the public logger methods reject invalid programmer inputs.
    /// </summary>
    [Fact]
    public async Task EntryPoints_InvalidArguments_ThrowArgumentNullOrArgumentException()
    {
        #region implementation

        // Arrange
        var logger = new CommandRunLogger(TimeProvider.System);

        // Act and assert
        Assert.Throws<ArgumentNullException>(() => new CommandRunLogger(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => logger.WriteStartedAsync(null, " ", TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => logger.WriteResultAsync(null, null!, TestContext.Current.CancellationToken));

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one isolated directory owned by the current test.</summary>
    /// <returns>The absolute temporary directory path.</returns>
    private static string createTemporaryDirectory()
    {
        #region implementation

        var path = Path.Combine(Path.GetTempPath(), $"carrot-command-run-logger-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;

        #endregion
    }

    /**************************************************************/
    /// <summary>Deletes one exact test-owned temporary directory.</summary>
    /// <param name="path">The owned directory path.</param>
    private static void deleteTemporaryDirectory(string path)
    {
        #region implementation

        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Provides a deterministic UTC clock for log-record assertions.</summary>
    private sealed class FixedTimeProvider : TimeProvider
    {
        #region implementation

        private readonly DateTimeOffset _utcNow;

        /**************************************************************/
        /// <summary>Initializes the fixed clock with one UTC instant.</summary>
        /// <param name="utcNow">The instant returned by <see cref="GetUtcNow"/>.</param>
        internal FixedTimeProvider(DateTimeOffset utcNow)
        {
            #region implementation

            _utcNow = utcNow;

            #endregion
        }

        /**************************************************************/
        /// <summary>Gets the configured deterministic UTC instant.</summary>
        /// <returns>The configured UTC instant.</returns>
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
