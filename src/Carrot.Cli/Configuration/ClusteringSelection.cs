namespace Carrot.Cli.Configuration;

/**************************************************************/
/// <summary>
/// Carries one caller's optional clustering selections before defaults and parameter content are resolved.
/// </summary>
/// <remarks>
/// Named commands populate this model from their options. Interactive processing uses an empty
/// instance so the validated application algorithm and language defaults remain in effect.
/// </remarks>
/// <seealso cref="ClusteringConfiguration"/>
/// <seealso cref="ClusteringConfigurationResolver"/>
internal sealed record ClusteringSelection
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the optional exact algorithm identifier requested by the caller.</summary>
    public string? Algorithm { get; init; }

    /**************************************************************/
    /// <summary>Gets the optional exact language identifier requested by the caller.</summary>
    public string? Language { get; init; }

    /**************************************************************/
    /// <summary>Gets the optional exact server-side template identifier requested by the caller.</summary>
    public string? Template { get; init; }

    /**************************************************************/
    /// <summary>Gets the optional existing JSON file containing algorithm parameter overrides.</summary>
    public string? ParametersFile { get; init; }

    #endregion
}
