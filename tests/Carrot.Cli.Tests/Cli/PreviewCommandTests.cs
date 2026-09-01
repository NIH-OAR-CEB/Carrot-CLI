using Carrot.Cli.Cli.Commands;
using Carrot.Cli.Cli.DependencyInjection;
using Carrot.Cli.Common;
using Carrot.Cli.Composition;
using Carrot.Cli.Processing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli.Testing;
using Spectre.Console.Testing;
using Xunit;

namespace Carrot.Cli.Tests.Cli;

/**************************************************************/
/// <summary>
/// Verifies named preview settings resolution, workflow execution, reporting, and cancellation mapping.
/// </summary>
public sealed class PreviewCommandTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Verifies valid named settings invoke the preview workflow without an interactive prompt.</summary>
    [Fact]
    public async Task Preview_ValidSettings_InvokesWorkflowAndReturnsItsExitCode()
    {
        #region implementation

        // Arrange
        var inputPath = createInputFile();
        var workflow = new RecordingWorkflow
        {
            Result = new ProcessRunResult
            {
                RunId = Guid.Parse("02d2f4d7-a2d0-4ec7-91ce-9ad2de15f1d1"),
                Status = OperationStatus.PartialSuccess,
                ExitCode = ExitCodes.PartialSuccess,
                Messages = [warning("preparation.extract", "One source was skipped.")],
                ArtifactPaths = [Path.Combine(Path.GetTempPath(), "preview.request.json")]
            }
        };
        var tester = createCommandTester(workflow, "https://environment.example/service");
        tester.Configure(configuration => configuration.AddCommand<PreviewCommand>("preview"));

        // Act
        var result = await tester.RunAsync(
            ["preview", "--input", inputPath, "--endpoint", "https://explicit.example/service", "--recursive"],
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ExitCodes.PartialSuccess, result.ExitCode);
        Assert.NotNull(workflow.Request);
        var request = workflow.Request!;
        Assert.Equal(inputPath, request.InputPath);
        Assert.Equal("https://explicit.example/service", request.Endpoint.AbsoluteUri);
        Assert.True(request.Recursive);
        Assert.Contains("Warning [preparation.extract]", result.Output, StringComparison.Ordinal);
        Assert.Contains("Artifacts:", result.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies invalid noninteractive settings report before the preview workflow is called.</summary>
    [Fact]
    public async Task Preview_MissingEndpoint_ReturnsConfigurationFailureWithoutWorkflowCall()
    {
        #region implementation

        // Arrange
        var workflow = new RecordingWorkflow();
        var tester = createCommandTester(workflow);
        tester.Configure(configuration => configuration.AddCommand<PreviewCommand>("preview"));

        // Act
        var result = await tester.RunAsync(
            ["preview", "--input", createInputFile()],
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ExitCodes.InvalidConfiguration, result.ExitCode);
        Assert.Null(workflow.Request);
        Assert.Contains("Error [endpoint.missing]", result.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies workflow cancellation does not emit a misleading terminal run summary.</summary>
    [Fact]
    public async Task Preview_CancelledWorkflow_ReturnsCancellationWithoutSummary()
    {
        #region implementation

        // Arrange
        var workflow = new RecordingWorkflow
        {
            Result = new ProcessRunResult { Status = OperationStatus.Failure, ExitCode = ExitCodes.Cancellation }
        };
        var tester = createCommandTester(workflow, "https://carrot.example/service");
        tester.Configure(configuration => configuration.AddCommand<PreviewCommand>("preview"));

        // Act
        var result = await tester.RunAsync(
            ["preview", "--input", createInputFile()],
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ExitCodes.Cancellation, result.ExitCode);
        Assert.NotNull(workflow.Request);
        Assert.DoesNotContain("Run ID:", result.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a noninteractive preview command tester with one controlled workflow dependency.</summary>
    private static CommandAppTester createCommandTester(RecordingWorkflow workflow, string? endpoint = null)
    {
        #region implementation

        var values = endpoint is null
            ? new Dictionary<string, string?>()
            : new Dictionary<string, string?> { ["CARROTCLI_ENDPOINT"] = endpoint };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection().AddCarrotCli(configuration);
        services.AddSingleton<IDocumentProcessingWorkflow>(workflow);
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = false;
        return new CommandAppTester(
            new TypeRegistrar(services),
            new CommandAppTesterSettings { TrimConsoleOutput = false },
            console);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a supported temporary text input for settings validation.</summary>
    private static string createInputFile()
    {
        #region implementation

        var path = Path.Combine(Path.GetTempPath(), $"carrot-preview-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "preview test input");
        return path;

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one safe warning message for controlled partial-preview output.</summary>
    private static OperationMessage warning(string code, string message)
    {
        #region implementation

        return new OperationMessage { Code = code, Message = message, Severity = OperationMessageSeverity.Warning };

        #endregion
    }

    /**************************************************************/
    /// <summary>Records the named request and returns the controlled terminal preview result.</summary>
    private sealed class RecordingWorkflow : IDocumentProcessingWorkflow
    {
        #region implementation

        /**************************************************************/
        /// <summary>Gets or sets the terminal result returned by preview execution.</summary>
        internal ProcessRunResult Result { get; init; } = new()
        {
            Status = OperationStatus.Success,
            ExitCode = ExitCodes.Success
        };

        /**************************************************************/
        /// <summary>Gets the resolved preview request received from the command.</summary>
        internal PreviewRequest? Request { get; private set; }

        /**************************************************************/
        /// <summary>Rejects processing because this test double supports preview only.</summary>
        public Task<ProcessRunResult> ProcessAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            #region implementation

            throw new InvalidOperationException("This test double supports preview only.");

            #endregion
        }

        /**************************************************************/
        /// <summary>Records the resolved preview request and returns the configured result.</summary>
        public Task<ProcessRunResult> PreviewAsync(PreviewRequest request, CancellationToken cancellationToken)
        {
            #region implementation

            Request = request;
            return Task.FromResult(Result);

            #endregion
        }

        #endregion
    }

    #endregion
}
