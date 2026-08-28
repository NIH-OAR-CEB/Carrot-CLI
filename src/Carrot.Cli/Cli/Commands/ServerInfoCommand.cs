using Carrot.Cli.CarrotApi;
using Carrot.Cli.Cli.Settings;
using Carrot.Cli.Cli.UI;
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

    /**************************************************************/
    /// <summary>
    /// Initializes the command with API, endpoint, and presentation boundaries.
    /// </summary>
    /// <param name="client">The Carrot API client.</param>
    /// <param name="endpointResolver">The service endpoint resolver.</param>
    /// <param name="reporter">The console reporter.</param>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    internal ServerInfoCommand(ICarrotApiClient client, EndpointResolver endpointResolver, ConsoleReporter reporter)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

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
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    protected override Task<int> ExecuteAsync(CommandContext context, EndpointSettings settings, CancellationToken cancellationToken)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
