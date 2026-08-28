using Spectre.Console.Cli;

namespace Carrot.Cli.Cli.Settings;

/**************************************************************/
/// <summary>
/// Defines command settings shared by operations that discover input documents.
/// </summary>
/// <seealso cref="ClusteringSettings"/>
internal class InputSettings : CommandSettings
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Gets or initializes the folder or ZIP archive supplied as the input container.
    /// </summary>
    [CommandOption("--input <PATH>")]
    public string? InputPath { get; init; }

    /**************************************************************/
    /// <summary>
    /// Gets or initializes whether folder discovery includes nested directories.
    /// </summary>
    [CommandOption("--recursive")]
    public bool Recursive { get; init; }

    #endregion
}
