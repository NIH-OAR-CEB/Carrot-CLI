using Spectre.Console.Cli;

namespace Carrot.Cli.Cli.Settings;

/**************************************************************/
/// <summary>
/// Defines noninteractive endpoint options for configuration inspection commands.
/// </summary>
internal class EndpointSettings : CommandSettings
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Gets or initializes the Carrot service base endpoint.
    /// </summary>
    [CommandOption("--endpoint <URI>")]
    public string? Endpoint { get; init; }

    /**************************************************************/
    /// <summary>
    /// Gets or initializes the request timeout override in seconds.
    /// </summary>
    [CommandOption("--timeout-seconds <SECONDS>")]
    public int? TimeoutSeconds { get; init; }

    #endregion
}
