using Spectre.Console.Cli;

namespace Carrot.Cli.Cli.Settings;

/**************************************************************/
/// <summary>
/// Defines endpoint and clustering selections shared by processing and preview commands.
/// </summary>
/// <seealso cref="InputSettings"/>
/// <seealso cref="ProcessSettings"/>
internal class ClusteringSettings : InputSettings
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
    /// Gets or initializes the algorithm identifier returned by the Carrot list endpoint.
    /// </summary>
    [CommandOption("--algorithm <NAME>")]
    public string? Algorithm { get; init; }

    /**************************************************************/
    /// <summary>
    /// Gets or initializes the language identifier returned by the Carrot list endpoint.
    /// </summary>
    [CommandOption("--language <NAME>")]
    public string? Language { get; init; }

    /**************************************************************/
    /// <summary>
    /// Gets or initializes the optional server-side request template name.
    /// </summary>
    [CommandOption("--template <NAME>")]
    public string? Template { get; init; }

    /**************************************************************/
    /// <summary>
    /// Gets or initializes the JSON file containing algorithm parameter overrides.
    /// </summary>
    [CommandOption("--parameters-file <PATH>")]
    public string? ParametersFile { get; init; }

    /**************************************************************/
    /// <summary>
    /// Gets or initializes the request timeout override in seconds.
    /// </summary>
    [CommandOption("--timeout-seconds <SECONDS>")]
    public int? TimeoutSeconds { get; init; }

    #endregion
}
