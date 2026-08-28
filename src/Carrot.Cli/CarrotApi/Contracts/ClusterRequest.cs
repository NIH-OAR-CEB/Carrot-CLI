using System.Text.Json;
using System.Text.Json.Serialization;

namespace Carrot.Cli.CarrotApi.Contracts;

/**************************************************************/
/// <summary>
/// Models the Carrot 4.8.6 <c>/cluster</c> request body from the authoritative OpenAPI schema.
/// </summary>
/// <seealso cref="ClusterDocument"/>
internal sealed record ClusterRequest
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the optional language identifier, which a named template may supply.</summary>
    [JsonPropertyName("language")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Language { get; init; }

    /**************************************************************/
    /// <summary>Gets the optional algorithm identifier, which a named template may supply.</summary>
    [JsonPropertyName("algorithm")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Algorithm { get; init; }

    /**************************************************************/
    /// <summary>Gets arbitrary algorithm parameter overrides represented as JSON values.</summary>
    [JsonPropertyName("parameters")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, JsonElement>? Parameters { get; init; }

    /**************************************************************/
    /// <summary>Gets all valid input documents in deterministic zero-based order.</summary>
    [JsonPropertyName("documents")]
    public IReadOnlyList<ClusterDocument> Documents { get; init; } = Array.Empty<ClusterDocument>();

    #endregion
}
