using System.Text.Json.Serialization;

namespace Carrot.Cli.CarrotApi.Contracts;

/**************************************************************/
/// <summary>
/// Models one labeled Carrot cluster node, its document indexes, score, and nested clusters.
/// </summary>
/// <seealso cref="ClusterResponse"/>
internal sealed record ClusterNode
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets one or more labels associated with this cluster node.</summary>
    [JsonPropertyName("labels")]
    public IReadOnlyList<string> Labels { get; init; } = Array.Empty<string>();

    /**************************************************************/
    /// <summary>Gets zero-based request document indexes associated with this node.</summary>
    [JsonPropertyName("documents")]
    public IReadOnlyList<int> Documents { get; init; } = Array.Empty<int>();

    /**************************************************************/
    /// <summary>Gets the numeric score associated with this node.</summary>
    [JsonPropertyName("score")]
    public double? Score { get; init; }

    /**************************************************************/
    /// <summary>Gets the possibly empty nested cluster collection.</summary>
    [JsonPropertyName("clusters")]
    public IReadOnlyList<ClusterNode> Clusters { get; init; } = Array.Empty<ClusterNode>();

    #endregion
}
