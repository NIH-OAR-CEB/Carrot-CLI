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
/// <summary>Verifies named process settings resolution, execution, and cancellation exit mapping.</summary>
public sealed class ProcessCommandTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Verifies valid settings invoke process without prompts and return the workflow exit code.</summary>
    [Fact]
    public async Task Process_ValidSettings_InvokesWorkflowAndReturnsItsExitCode()
    {
        #region implementation

        // Arrange
        var workflow = new RecordingWorkflow
        {
            Result = new ProcessRunResult
            {
                RunId = Guid.Parse("02d2f4d7-a2d0-4ec7-91ce-9ad2de15f1d1"),
                Status = OperationStatus.PartialSuccess,
                ExitCode = ExitCodes.PartialSuccess,
                ArtifactPaths = [Path.Combine(Path.GetTempPath(), "results.xlsx")]
            }
        };
        var tester = createCommandTester(workflow);
        tester.Configure(configuration => configuration.AddCommand<ProcessCommand>("process"));
        var inputPath = createInputFile();

        // Act
        var result = await tester.RunAsync(
            ["process", "--input", inputPath, "--endpoint", "https://explicit.example/service", "--no-json-artifacts"],
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ExitCodes.PartialSuccess, result.ExitCode);
        var request = Assert.IsType<ProcessRequest>(workflow.Request);
        Assert.Equal(inputPath, request.InputPath);
        Assert.Equal("https://explicit.example/service", request.Endpoint.AbsoluteUri);
        Assert.False(request.WriteJsonArtifacts);

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies process cancellation does not emit a terminal run summary.</summary>
    [Fact]
    public async Task Process_CancelledWorkflow_ReturnsCancellationWithoutSummary()
    {
        #region implementation

        // Arrange
        var workflow = new RecordingWorkflow
        {
            Result = new ProcessRunResult { Status = OperationStatus.Failure, ExitCode = ExitCodes.Cancellation }
        };
        var tester = createCommandTester(workflow);
        tester.Configure(configuration => configuration.AddCommand<ProcessCommand>("process"));

        // Act
        var result = await tester.RunAsync(
            ["process", "--input", createInputFile(), "--endpoint", "https://carrot.example/service"],
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ExitCodes.Cancellation, result.ExitCode);
        Assert.DoesNotContain("Run ID:", result.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a process command tester with a controlled workflow dependency.</summary>
    private static CommandAppTester createCommandTester(RecordingWorkflow workflow)
    {
        var services = new ServiceCollection().AddCarrotCli(new ConfigurationBuilder().Build());
        services.AddSingleton<IDocumentProcessingWorkflow>(workflow);
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = false;
        return new CommandAppTester(new TypeRegistrar(services), new CommandAppTesterSettings { TrimConsoleOutput = false }, console);
    }

    /**************************************************************/
    /// <summary>Creates a supported temporary text input for settings validation.</summary>
    private static string createInputFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"carrot-process-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "process test input");
        return path;
    }

    /**************************************************************/
    /// <summary>Records named process requests and returns a controlled terminal result.</summary>
    private sealed class RecordingWorkflow : IDocumentProcessingWorkflow
    {
        /**************************************************************/
        /// <summary>Gets or sets the terminal process result.</summary>
        internal ProcessRunResult Result { get; init; } = new() { Status = OperationStatus.Success, ExitCode = ExitCodes.Success };

        /**************************************************************/
        /// <summary>Gets the resolved process request received from the command.</summary>
        internal ProcessRequest? Request { get; private set; }

        /**************************************************************/
        /// <summary>Records and returns the configured processing result.</summary>
        public Task<ProcessRunResult> ProcessAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(Result);
        }

        /**************************************************************/
        /// <summary>Rejects preview because this test double supports process only.</summary>
        public Task<ProcessRunResult> PreviewAsync(PreviewRequest request, CancellationToken cancellationToken) => throw new InvalidOperationException();
    }

    #endregion
}
