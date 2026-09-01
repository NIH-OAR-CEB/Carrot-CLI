using System.Text.Json;

namespace Carrot.Cli.Configuration;

/**************************************************************/
/// <summary>
/// Carries one immutable clustering configuration after selection semantics and parameters are resolved.
/// </summary>
/// <remarks>
/// Algorithm and language are present together for a direct selection. They are both absent when
/// <see cref="Template"/> delegates those choices to a named Carrot template. Parameter values are
/// detached <see cref="JsonElement"/> clones and remain valid after the source document is disposed.
/// </remarks>
/// <seealso cref="ClusteringSelection"/>
/// <seealso cref="ClusteringConfigurationResolver"/>
internal sealed record ClusteringConfiguration
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the exact algorithm identifier, or <see langword="null"/> for template selection.</summary>
    public string? Algorithm { get; init; }

    /**************************************************************/
    /// <summary>Gets the exact language identifier, or <see langword="null"/> for template selection.</summary>
    public string? Language { get; init; }

    /**************************************************************/
    /// <summary>Gets the exact server-side template identifier, or <see langword="null"/> for direct selection.</summary>
    public string? Template { get; init; }

    /**************************************************************/
    /// <summary>Gets detached arbitrary JSON parameter overrides, or <see langword="null"/> when omitted.</summary>
    /// <remarks>
    /// A supplied empty JSON object is retained as an empty dictionary rather than changed to
    /// <see langword="null"/>, preserving the distinction between an omitted file and <c>{}</c>.
    /// </remarks>
    public IReadOnlyDictionary<string, JsonElement>? Parameters { get; init; }

    #endregion
}
