using System.Text.Json;
using Carrot.Cli.CarrotApi;
using Carrot.Cli.CarrotApi.Contracts;
using Carrot.Cli.Cli.Commands;
using Carrot.Cli.Cli.DependencyInjection;
using Carrot.Cli.Common;
using Carrot.Cli.Composition;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli.Testing;
using Spectre.Console.Testing;
using Xunit;

namespace Carrot.Cli.Tests.Cli;

/**************************************************************/
/// <summary>
/// Verifies named server-information endpoint resolution, deterministic output, failure mapping, and cancellation.
/// </summary>
public sealed class ServerInfoCommandTests
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Verifies an explicit endpoint wins over the environment value and server values render in ordinal order.
    /// </summary>
    [Fact]
    public async Task ServerInfo_ExplicitEndpoint_ReturnsSortedConfiguration()
    {
        #region implementation

        // Arrange
        var client = new RecordingApiClient();
        client.GetConfiguration = (_, _, _, _) => Task.FromResult(OperationResult<ListResponse>.Success(
            new ListResponse
            {
                Algorithms = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
                {
                    ["STC"] = ["French", "English"],
                    ["Lingo"] = ["English"]
                },
                Templates = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                {
                    ["zebra"] = JsonSerializer.SerializeToElement(new { algorithm = "STC" }),
                    ["alpha"] = JsonSerializer.SerializeToElement(new { algorithm = "Lingo" })
                }
            }));
        var tester = createCommandTester(client, "https://environment.example/service");
        tester.Configure(configuration => configuration.AddCommand<ServerInfoCommand>("server-info"));

        // Act
        var result = await tester.RunAsync(
            ["server-info", "--endpoint", "https://explicit.example/service", "--timeout-seconds", "9"],
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, result.ExitCode);
        var request = Assert.Single(client.ConfigurationRequests);
        Assert.Equal("https://explicit.example/service", request.Endpoint.AbsoluteUri);
        Assert.Equal(TimeSpan.FromSeconds(9), request.Timeout);
        Assert.Null(request.Indent);
        Assert.Contains("Algorithms and languages:", result.Output, StringComparison.Ordinal);
        Assert.True(result.Output.IndexOf("Lingo: English", StringComparison.Ordinal)
            < result.Output.IndexOf("STC: English, French", StringComparison.Ordinal));
        Assert.True(result.Output.IndexOf("alpha", StringComparison.Ordinal)
            < result.Output.IndexOf("zebra", StringComparison.Ordinal));
        Assert.DoesNotContain("algorithm =", result.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies an omitted endpoint uses the environment fallback and configured timeout without prompting.
    /// </summary>
    [Fact]
    public async Task ServerInfo_EnvironmentEndpoint_UsesConfiguredTimeout()
    {
        #region implementation

        // Arrange
        var client = new RecordingApiClient();
        client.GetConfiguration = (_, _, _, _) => Task.FromResult(OperationResult<ListResponse>.Success(emptyResponse()));
        var tester = createCommandTester(client, "https://environment.example/service", timeoutSeconds: 17);
        tester.Configure(configuration => configuration.AddCommand<ServerInfoCommand>("server-info"));

        // Act
        var result = await tester.RunAsync(["server-info"], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, result.ExitCode);
        var request = Assert.Single(client.ConfigurationRequests);
        Assert.Equal("https://environment.example/service", request.Endpoint.AbsoluteUri);
        Assert.Equal(TimeSpan.FromSeconds(17), request.Timeout);
        Assert.Contains("  (none)", result.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies invalid endpoint settings return configuration failure before an HTTP call.
    /// </summary>
    [Fact]
    public async Task ServerInfo_InvalidEndpoint_ReturnsConfigurationFailureWithoutHttpCall()
    {
        #region implementation

        // Arrange
        var client = new RecordingApiClient();
        var tester = createCommandTester(client);
        tester.Configure(configuration => configuration.AddCommand<ServerInfoCommand>("server-info"));

        // Act
        var result = await tester.RunAsync(["server-info"], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, result.ExitCode);
        Assert.Empty(client.ConfigurationRequests);
        Assert.Contains("Error [endpoint.missing]", result.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies endpoint, redirect, and malformed-response failures use the stable endpoint exit code.
    /// </summary>
    /// <param name="failureCode">The API diagnostic code returned by the fake client.</param>
    [Theory]
    [InlineData("carrot.http")]
    [InlineData("carrot.redirect")]
    [InlineData("carrot.response.malformed")]
    public async Task ServerInfo_ApiFailure_ReturnsEndpointFailure(string failureCode)
    {
        #region implementation

        // Arrange
        var client = new RecordingApiClient();
        client.GetConfiguration = (_, _, _, _) => Task.FromResult(OperationResult<ListResponse>.Failure(
            [new OperationMessage
            {
                Code = failureCode,
                Message = "Safe endpoint failure.",
                Severity = OperationMessageSeverity.Error
            }]));
        var tester = createCommandTester(client, "https://carrot.example/service");
        tester.Configure(configuration => configuration.AddCommand<ServerInfoCommand>("server-info"));

        // Act
        var result = await tester.RunAsync(["server-info"], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(4, result.ExitCode);
        Assert.Single(client.ConfigurationRequests);
        Assert.Contains($"Error [{failureCode}]", result.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies caller cancellation returns the documented cancellation exit code without rendering a failure.
    /// </summary>
    [Fact]
    public async Task ServerInfo_CancelledRequest_ReturnsCancellation()
    {
        #region implementation

        // Arrange
        var client = new RecordingApiClient();
        client.GetConfiguration = (_, _, _, cancellationToken) =>
        {
            var cancellationSource = new CancellationTokenSource();
            cancellationSource.Cancel();
            throw new OperationCanceledException(cancellationSource.Token);
        };
        var tester = createCommandTester(client, "https://carrot.example/service");
        tester.Configure(configuration => configuration.AddCommand<ServerInfoCommand>("server-info"));

        // Act
        var result = await tester.RunAsync(["server-info"], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(130, result.ExitCode);
        Assert.Single(client.ConfigurationRequests);
        Assert.DoesNotContain("Error", result.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Creates one noninteractive command harness using the production service registrations.
    /// </summary>
    /// <param name="client">The controlled API client for the command under test.</param>
    /// <param name="endpoint">The optional environment endpoint fallback.</param>
    /// <param name="timeoutSeconds">The configured default timeout.</param>
    /// <returns>A configured named-command test harness.</returns>
    private static CommandAppTester createCommandTester(
        RecordingApiClient client,
        string? endpoint = null,
        int? timeoutSeconds = null)
    {
        #region implementation

        var values = new Dictionary<string, string?>();
        if (endpoint is not null)
        {
            values["CARROTCLI_ENDPOINT"] = endpoint;
        }

        if (timeoutSeconds is not null)
        {
            values["CarrotCli:HttpTimeoutSeconds"] = timeoutSeconds.Value.ToString();
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection().AddCarrotCli(configuration);
        services.AddSingleton<ICarrotApiClient>(client);
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = false;
        return new CommandAppTester(
            new TypeRegistrar(services),
            new CommandAppTesterSettings { TrimConsoleOutput = false },
            console);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Creates an otherwise-empty valid list response.
    /// </summary>
    /// <returns>A list response containing empty algorithm and template maps.</returns>
    private static ListResponse emptyResponse()
    {
        #region implementation

        return new ListResponse
        {
            Algorithms = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal),
            Templates = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Provides a controllable in-memory Carrot API client and records list-operation arguments.
    /// </summary>
    private sealed class RecordingApiClient : ICarrotApiClient
    {
        #region implementation

        /**************************************************************/
        /// <summary>Gets or sets the behavior executed for each list operation.</summary>
        internal Func<Uri, TimeSpan, bool?, CancellationToken, Task<OperationResult<ListResponse>>> GetConfiguration { get; set; }
            = (_, _, _, _) => Task.FromResult(OperationResult<ListResponse>.Success(emptyResponse()));

        /**************************************************************/
        /// <summary>Gets recorded list-operation arguments in invocation order.</summary>
        internal List<(Uri Endpoint, TimeSpan Timeout, bool? Indent)> ConfigurationRequests { get; } = [];

        /**************************************************************/
        /// <summary>
        /// Records and executes the configured list operation behavior.
        /// </summary>
        public Task<OperationResult<ListResponse>> GetConfigurationAsync(
            Uri serviceEndpoint,
            TimeSpan timeout,
            bool? indent,
            CancellationToken cancellationToken)
        {
            #region implementation

            ConfigurationRequests.Add((serviceEndpoint, timeout, indent));
            return GetConfiguration(serviceEndpoint, timeout, indent, cancellationToken);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Rejects cluster calls because server-information must call only the list endpoint.
        /// </summary>
        public Task<OperationResult<ClusterResponse>> ClusterAsync(
            Uri serviceEndpoint,
            ClusterRequest request,
            string? template,
            TimeSpan timeout,
            bool? indent,
            CancellationToken cancellationToken)
        {
            #region implementation

            throw new InvalidOperationException("Server information must not call the cluster endpoint.");

            #endregion
        }

        #endregion
    }

    #endregion
}
