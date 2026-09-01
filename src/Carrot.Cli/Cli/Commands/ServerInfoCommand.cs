using Carrot.Cli.CarrotApi;
using Carrot.Cli.Cli.Settings;
using Carrot.Cli.Cli.UI;
using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Carrot.Cli.Processing;
using Spectre.Console.Cli;

namespace Carrot.Cli.Cli.Commands;

/**************************************************************/
/// <summary>
/// Defines the noninteractive command that displays algorithms, languages, and templates.
/// </summary>
/// <seealso cref="ICarrotApiClient.GetConfigurationAsync"/>
internal sealed class ServerInfoCommand : AsyncCommand<EndpointSettings>
{
    #region implementation

    private readonly ICarrotApiClient _client;
    private readonly RunSettingsResolver _settingsResolver;
    private readonly ConsoleReporter _reporter;

    /**************************************************************/
    /// <summary>
    /// Initializes the command with API, endpoint, and presentation boundaries.
    /// </summary>
    /// <param name="client">The Carrot API client.</param>
    /// <param name="settingsResolver">The noninteractive endpoint and timeout resolver.</param>
    /// <param name="reporter">The console reporter.</param>
    /// <exception cref="ArgumentNullException">Thrown when a required dependency is null.</exception>
    public ServerInfoCommand(ICarrotApiClient client, RunSettingsResolver settingsResolver, ConsoleReporter reporter)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(settingsResolver);
        ArgumentNullException.ThrowIfNull(reporter);

        _client = client;
        _settingsResolver = settingsResolver;
        _reporter = reporter;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Queries the list endpoint and renders its configuration without prompting.
    /// </summary>
    /// <param name="context">The Spectre command execution context.</param>
    /// <param name="settings">The endpoint settings.</param>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task containing the endpoint-related exit code.</returns>
    protected override async Task<int> ExecuteAsync(CommandContext context, EndpointSettings settings, CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(settings);

        var settingsResult = _settingsResolver.ResolveEndpointSettings(settings);
        if (settingsResult.Status == OperationStatus.Failure)
        {
            _reporter.WriteMessages(settingsResult.Messages);
            return ExitCodes.InvalidConfiguration;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resolvedSettings = settingsResult.Value!;
            var configurationResult = await _client.GetConfigurationAsync(
                resolvedSettings.Endpoint,
                resolvedSettings.Timeout,
                indent: null,
                cancellationToken).ConfigureAwait(false);

            if (configurationResult.Status == OperationStatus.Failure)
            {
                _reporter.WriteMessages(configurationResult.Messages);
                return ExitCodes.EndpointFailure;
            }

            _reporter.WriteServerInfo(configurationResult.Value!);
            return ExitCodes.Success;
        }
        catch (OperationCanceledException)
        {
            return ExitCodes.Cancellation;
        }

        #endregion
    }

    #endregion
}
