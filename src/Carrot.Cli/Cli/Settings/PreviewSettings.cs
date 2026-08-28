using Spectre.Console.Cli;

namespace Carrot.Cli.Cli.Settings;

/**************************************************************/
/// <summary>
/// Defines noninteractive options for discovery, extraction, and request preview only.
/// </summary>
/// <seealso cref="ClusteringSettings"/>
internal sealed class PreviewSettings : ClusteringSettings
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Gets or initializes the destination for the serialized request preview artifact.
    /// </summary>
    [CommandOption("--output <PATH>")]
    public string? OutputPath { get; init; }

    /**************************************************************/
    /// <summary>
    /// Gets or initializes whether an existing preview artifact may be replaced.
    /// </summary>
    [CommandOption("--overwrite")]
    public bool Overwrite { get; init; }

    #endregion
}
