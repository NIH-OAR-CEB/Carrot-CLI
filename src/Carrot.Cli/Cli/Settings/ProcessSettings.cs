using Spectre.Console.Cli;

namespace Carrot.Cli.Cli.Settings;

/**************************************************************/
/// <summary>
/// Defines all noninteractive options for a complete clustering and reporting run.
/// </summary>
/// <seealso cref="ClusteringSettings"/>
internal sealed class ProcessSettings : ClusteringSettings
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Gets or initializes an explicit workbook output path or output directory.
    /// </summary>
    [CommandOption("--output <PATH>")]
    public string? OutputPath { get; init; }

    /**************************************************************/
    /// <summary>
    /// Gets or initializes whether existing output artifacts may be replaced.
    /// </summary>
    [CommandOption("--overwrite")]
    public bool Overwrite { get; init; }

    /**************************************************************/
    /// <summary>
    /// Gets or initializes whether request and response JSON sidecars are suppressed.
    /// </summary>
    [CommandOption("--no-json-artifacts")]
    public bool NoJsonArtifacts { get; init; }

    /**************************************************************/
    /// <summary>
    /// Gets or initializes whether normal console progress and summaries are suppressed.
    /// </summary>
    [CommandOption("--quiet")]
    public bool Quiet { get; init; }

    /**************************************************************/
    /// <summary>
    /// Gets or initializes an explicit diagnostic log path.
    /// </summary>
    [CommandOption("--log-file <PATH>")]
    public string? LogFile { get; init; }

    #endregion
}
