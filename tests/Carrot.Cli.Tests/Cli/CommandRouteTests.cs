using Carrot.Cli.Cli;
using Carrot.Cli.Cli.Commands;
using Carrot.Cli.Cli.DependencyInjection;
using Carrot.Cli.Cli.UI;
using Carrot.Cli.CarrotApi;
using Carrot.Cli.Composition;
using Carrot.Cli.Configuration;
using Carrot.Cli.Processing;
using Carrot.Cli.Reporting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli.Testing;
using Spectre.Console.Testing;
using Xunit;

namespace Carrot.Cli.Tests.Cli;

/**************************************************************/
/// <summary>
/// Verifies named help/about commands, generated CLI metadata, and production DI resolution.
/// </summary>
public sealed class CommandRouteTests
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Verifies the named help command renders a normalized topic and returns success.
    /// </summary>
    [Fact]
    public void Help_KnownTopic_ReturnsSuccessAndRendersMarkdown()
    {
        #region implementation

        // Arrange
        var tester = createCommandTester();
        tester.Configure(configuration => configuration.AddCommand<HelpCommand>("help"));

        // Act
        var result = tester.Run("help", "Output_Format");

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Output Columns", result.Output, StringComparison.Ordinal);
        Assert.Contains("CategoryMembershipsJson", result.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies the named help command returns invalid configuration for an unknown topic.
    /// </summary>
    [Fact]
    public void Help_UnknownTopic_ReturnsInvalidConfiguration()
    {
        #region implementation

        // Arrange
        var tester = createCommandTester();
        tester.Configure(configuration => configuration.AddCommand<HelpCommand>("help"));

        // Act
        var result = tester.Run("help", "unknown-topic");

        // Assert
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Unknown help topic", result.Output, StringComparison.Ordinal);
        Assert.Contains("Available topics", result.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies the named About command renders application metadata and returns success.
    /// </summary>
    [Fact]
    public void About_Always_ReturnsSuccessAndRendersMetadata()
    {
        #region implementation

        // Arrange
        var tester = createCommandTester();
        tester.Configure(configuration => configuration.AddCommand<AboutCommand>("about"));

        // Act
        var result = tester.Run("about");

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Carrot CLI", result.Output, StringComparison.Ordinal);
        Assert.Contains("Carrot 4.8.6", result.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies the production command factory exposes the documented command names in generated help.
    /// </summary>
    [Fact]
    public async Task Factory_Help_ExposesConfiguredCommandMetadata()
    {
        #region implementation

        // Arrange
        var tester = createCommandTester();
        tester.SetDefaultCommand<InteractiveCommand>();
        tester.Configure(new CommandAppFactory().Configure);

        // Act
        var result = await tester.RunAsync(
            ["--help"],
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("process", result.Output, StringComparison.Ordinal);
        Assert.Contains("preview", result.Output, StringComparison.Ordinal);
        Assert.Contains("server-info", result.Output, StringComparison.Ordinal);
        Assert.Contains("help", result.Output, StringComparison.Ordinal);
        Assert.Contains("about", result.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies the production command factory exposes the application version route.
    /// </summary>
    [Fact]
    public async Task Factory_Version_ExposesConfiguredApplicationVersion()
    {
        #region implementation

        // Arrange
        var tester = createCommandTester();
        tester.SetDefaultCommand<InteractiveCommand>();
        tester.Configure(new CommandAppFactory().Configure);

        // Act
        var result = await tester.RunAsync(
            ["--version"],
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Matches(@"^\d+\.\d+\.\d+", result.Output);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies the production service collection can construct the complete interactive UI graph.
    /// </summary>
    [Fact]
    public void ServiceRegistration_InteractiveGraph_ResolvesPreparedProcessingServices()
    {
        #region implementation

        // Arrange
        using var console = new TestConsole();
        var services = createServices();
        _ = new CommandAppFactory().Create(services);
        services.AddSingleton<IAnsiConsole>(console);
        using var provider = services.BuildServiceProvider();

        // Act
        var menu = provider.GetRequiredService<InteractiveMenu>();

        // Assert
        Assert.NotNull(menu);
        Assert.NotNull(provider.GetRequiredService<HelpRenderer>());
        Assert.NotNull(provider.GetRequiredService<AboutRenderer>());
        Assert.NotNull(provider.GetRequiredService<ICarrotApiClient>());
        Assert.NotNull(provider.GetRequiredService<IPreparedDocumentProcessor>());
        Assert.NotNull(provider.GetRequiredService<EndpointResolver>());
        Assert.NotNull(provider.GetRequiredService<RunSettingsResolver>());
        Assert.NotNull(provider.GetRequiredService<ConsoleReporter>());
        Assert.NotNull(provider.GetRequiredService<ProcessedResultsPager>());
        Assert.NotNull(provider.GetRequiredService<IRunIdProvider>());
        Assert.NotNull(provider.GetRequiredService<ExcelOutputPathResolver>());
        Assert.NotNull(provider.GetRequiredService<ExcelOutputPathSuggester>());
        Assert.NotNull(provider.GetRequiredService<IExcelReportWriter>());
        Assert.NotNull(provider.GetRequiredService<IJsonArtifactWriter>());
        Assert.NotNull(provider.GetRequiredService<IProcessedResultsExporter>());
        Assert.NotNull(provider.GetRequiredService<ProcessedResultsExportFlow>());

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Creates a Spectre command tester backed by the production Microsoft DI registrations.
    /// </summary>
    /// <returns>The configured command test harness.</returns>
    private static CommandAppTester createCommandTester()
    {
        #region implementation

        var services = createServices();
        var console = new TestConsole();
        console.Profile.Height = 200;
        return new CommandAppTester(
            new TypeRegistrar(services),
            new CommandAppTesterSettings { TrimConsoleOutput = false },
            console);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Creates the production UI service collection against an empty in-memory configuration.
    /// </summary>
    /// <returns>The mutable service collection.</returns>
    private static IServiceCollection createServices()
    {
        #region implementation

        var configuration = new ConfigurationBuilder().Build();
        return new ServiceCollection().AddCarrotCli(configuration);

        #endregion
    }

    #endregion
}
