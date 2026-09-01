using System.Text.Json;
using Carrot.Cli.CarrotApi.Contracts;
using Carrot.Cli.Cli.UI;
using Carrot.Cli.Common;
using Carrot.Cli.Processing;
using Spectre.Console.Testing;
using Xunit;

namespace Carrot.Cli.Tests.Cli;

/**************************************************************/
/// <summary>
/// Verifies deterministic, quiet-aware, and markup-safe named-command console reporting.
/// </summary>
public sealed class ConsoleReporterTests
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Verifies normal run reporting preserves message and artifact order with a stable summary.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WriteRunResult_NormalOutput_WritesDeterministicSummary(bool interactive)
    {
        #region implementation

        // Arrange
        using var console = createConsole(interactive);
        var reporter = new ConsoleReporter(console);
        var result = new ProcessRunResult
        {
            RunId = Guid.Parse("11223344-5566-7788-99aa-bbccddeeff00"),
            Status = OperationStatus.PartialSuccess,
            ExitCode = 2,
            Messages =
            [
                message("run.started", "Started [safely].", OperationMessageSeverity.Information),
                message("file.skipped", "One file was skipped.", OperationMessageSeverity.Warning),
                message("artifact.failed", "One artifact failed.", OperationMessageSeverity.Error)
            ],
            ArtifactPaths = [@"C:\reports\[first].xlsx", @"C:\reports\second.json"]
        };

        // Act
        reporter.WriteRunResult(result, quiet: false);

        // Assert
        var output = console.Output.ReplaceLineEndings("\n");
        Assert.Equal(
            "Information [run.started]: Started [safely].\n"
            + "Warning [file.skipped]: One file was skipped.\n"
            + "Error [artifact.failed]: One artifact failed.\n"
            + "Run ID: 11223344-5566-7788-99aa-bbccddeeff00\n"
            + "Status: PartialSuccess\n"
            + "Exit code: 2\n"
            + "Rows: 0\n"
            + "Artifacts:\n"
            + "  C:\\reports\\[first].xlsx\n"
            + "  C:\\reports\\second.json\n",
            output);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies quiet mode suppresses informational and summary output but retains warnings and errors.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WriteRunResult_QuietOutput_RetainsWarningsAndErrorsOnly(bool interactive)
    {
        #region implementation

        // Arrange
        using var console = createConsole(interactive);
        var reporter = new ConsoleReporter(console);
        var result = new ProcessRunResult
        {
            Messages =
            [
                message("run.started", "Started.", OperationMessageSeverity.Information),
                message("file.skipped", "Skipped.", OperationMessageSeverity.Warning),
                message("run.failed", "Failed.", OperationMessageSeverity.Error)
            ],
            ArtifactPaths = ["suppressed.xlsx"]
        };

        // Act
        reporter.WriteRunResult(result, quiet: true);

        // Assert
        Assert.Equal(
            "Warning [file.skipped]: Skipped.\nError [run.failed]: Failed.\n",
            console.Output.ReplaceLineEndings("\n"));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies a quiet successful run without diagnostics produces no console output.
    /// </summary>
    [Fact]
    public void WriteRunResult_QuietSuccess_ProducesNoOutput()
    {
        #region implementation

        // Arrange
        using var console = createConsole(interactive: false);
        var reporter = new ConsoleReporter(console);

        // Act
        reporter.WriteRunResult(new ProcessRunResult(), quiet: true);

        // Assert
        Assert.Empty(console.Output);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies algorithms, languages, and templates are sorted identically on every console type.
    /// </summary>
    /// <param name="interactive">Whether the test console advertises interactive capabilities.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WriteServerInfo_UnsortedConfiguration_WritesDeterministicLiteralText(bool interactive)
    {
        #region implementation

        // Arrange
        using var console = createConsole(interactive);
        var reporter = new ConsoleReporter(console);
        var configuration = new ListResponse
        {
            Algorithms = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["Zeta[Algorithm]"] = ["Polish", "English"],
                ["Alpha"] = []
            },
            Templates = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["z-template"] = JsonSerializer.SerializeToElement(new { }),
                ["[a]-template"] = JsonSerializer.SerializeToElement(new { })
            }
        };

        // Act
        reporter.WriteServerInfo(configuration);

        // Assert
        Assert.Equal(
            "Algorithms and languages:\n"
            + "  Alpha: (none)\n"
            + "  Zeta[Algorithm]: English, Polish\n"
            + "Templates:\n"
            + "  [a]-template\n"
            + "  z-template\n",
            console.Output.ReplaceLineEndings("\n"));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies empty server collections are represented explicitly rather than omitted.
    /// </summary>
    [Fact]
    public void WriteServerInfo_EmptyConfiguration_WritesNoneMarkers()
    {
        #region implementation

        // Arrange
        using var console = createConsole(interactive: false);
        var reporter = new ConsoleReporter(console);
        var configuration = new ListResponse
        {
            Algorithms = new Dictionary<string, IReadOnlyList<string>>(),
            Templates = new Dictionary<string, JsonElement>()
        };

        // Act
        reporter.WriteServerInfo(configuration);

        // Assert
        Assert.Equal(
            "Algorithms and languages:\n  (none)\nTemplates:\n  (none)\n",
            console.Output.ReplaceLineEndings("\n"));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies the reporter constructor and both report methods reject null programmer inputs.
    /// </summary>
    [Fact]
    public void PublicAndInternalEntryPoints_NullValues_ThrowArgumentNullException()
    {
        #region implementation

        // Arrange
        using var console = createConsole(interactive: false);
        var reporter = new ConsoleReporter(console);

        // Act and assert
        Assert.Throws<ArgumentNullException>(() => new ConsoleReporter(null!));
        Assert.Throws<ArgumentNullException>(() => reporter.WriteRunResult(null!, quiet: false));
        Assert.Throws<ArgumentNullException>(() => reporter.WriteServerInfo(null!));

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one immutable operation-message fixture.</summary>
    /// <param name="code">The stable message code.</param>
    /// <param name="text">The safe message text.</param>
    /// <param name="severity">The message severity.</param>
    /// <returns>The operation-message fixture.</returns>
    private static OperationMessage message(string code, string text, OperationMessageSeverity severity)
    {
        #region implementation

        return new OperationMessage { Code = code, Message = text, Severity = severity };

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a wide test console with the selected interaction capability.</summary>
    /// <param name="interactive">Whether the console accepts interactive prompts.</param>
    /// <returns>The disposable test console.</returns>
    private static TestConsole createConsole(bool interactive)
    {
        #region implementation

        var console = new TestConsole();
        console.Profile.Width = 240;
        console.Profile.Capabilities.Interactive = interactive;
        return console;

        #endregion
    }

    #endregion
}
