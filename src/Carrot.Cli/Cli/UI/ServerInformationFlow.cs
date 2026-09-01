using Carrot.Cli.CarrotApi;
using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>
/// Coordinates interactive endpoint collection and display of Carrot server information.
/// </summary>
/// <remarks>
/// The flow keeps the endpoint in memory for one execution only. It validates the editable value
/// through the shared endpoint policy, calls only <c>/list</c>, and returns to its owning menu after
/// displaying either the configuration or safe diagnostics.
/// </remarks>
/// <seealso cref="EndpointResolver"/>
/// <seealso cref="ICarrotApiClient.GetConfigurationAsync"/>
/// <seealso cref="ConsoleReporter.WriteServerInfo"/>
internal sealed class ServerInformationFlow
{
    #region implementation

    private const string DefaultServiceEndpoint = "http://localhost:8080/service";

    private readonly IAnsiConsole _console;
    private readonly EndpointResolver _endpointResolver;
    private readonly ICarrotApiClient _client;
    private readonly ConsoleReporter _reporter;
    private readonly ServerInformationPager _pager;
    private readonly TimeSpan _httpTimeout;

    /**************************************************************/
    /// <summary>
    /// Initializes the interactive server-information flow with endpoint, API, and presentation boundaries.
    /// </summary>
    /// <param name="console">The interactive console used for prompting and status output.</param>
    /// <param name="endpointResolver">The shared strict service-endpoint validator.</param>
    /// <param name="client">The Carrot HTTP API client.</param>
    /// <param name="reporter">The shared literal-text server-information reporter.</param>
    /// <param name="pager">The terminal-height-aware server-information results pager.</param>
    /// <param name="options">The validated CLI options containing the HTTP timeout.</param>
    /// <exception cref="ArgumentNullException">Thrown when a required dependency is null.</exception>
    public ServerInformationFlow(
        IAnsiConsole console,
        EndpointResolver endpointResolver,
        ICarrotApiClient client,
        ConsoleReporter reporter,
        ServerInformationPager pager,
        IOptions<CarrotCliOptions> options)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(endpointResolver);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(reporter);
        ArgumentNullException.ThrowIfNull(pager);
        ArgumentNullException.ThrowIfNull(options);

        _console = console;
        _endpointResolver = endpointResolver;
        _client = client;
        _reporter = reporter;
        _pager = pager;
        _httpTimeout = TimeSpan.FromSeconds(options.Value.HttpTimeoutSeconds);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Prompts for an endpoint, retrieves server information, and returns to the owning submenu.
    /// </summary>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task representing the interactive server-information operation.</returns>
    /// <remarks>
    /// Invalid endpoint values remain in the prompt until corrected. API failures are rendered as
    /// safe diagnostics and do not terminate the parent interactive session. Cancellation is
    /// propagated to the parent menu so it can return the documented cancellation code.
    /// </remarks>
    /// <seealso cref="EndpointResolver.Resolve"/>
    /// <seealso cref="ICarrotApiClient.GetConfigurationAsync"/>
    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        #region implementation

        var endpointText = await new TextPrompt<string>(
                "Carrot service endpoint [grey](the value is not persisted)[/]:")
            .DefaultValue(DefaultServiceEndpoint)
            .PromptStyle("yellow")
            .Validate(value =>
            {
                var endpointResult = _endpointResolver.Resolve(value);
                return endpointResult.Value is null
                    ? ValidationResult.Error(endpointResult.Messages[0].Message)
                    : ValidationResult.Success();
            })
            .ShowAsync(_console, cancellationToken)
            .ConfigureAwait(false);
        var endpoint = _endpointResolver.Resolve(endpointText).Value!;

        _console.MarkupLine("[orange1]Loading Carrot server information...[/]");
        var result = await _client.GetConfigurationAsync(
            endpoint,
            _httpTimeout,
            indent: null,
            cancellationToken).ConfigureAwait(false);

        if (result.Status == OperationStatus.Failure)
        {
            _reporter.WriteMessages(result.Messages);
            return;
        }

        await _pager.ShowAsync(result.Value!, cancellationToken).ConfigureAwait(false);
        _console.WriteLine();

        #endregion
    }

    #endregion
}
