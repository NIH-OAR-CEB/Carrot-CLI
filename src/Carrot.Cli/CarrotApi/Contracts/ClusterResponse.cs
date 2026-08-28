using System.Text.Json.Serialization;

namespace Carrot.Cli.CarrotApi.Contracts;

/**************************************************************/
/// <summary>
/// Models the Carrot 4.8.6 <c>/cluster</c> response body.
/// </summary>
/// <seealso cref="ClusterNode"/>
internal sealed record ClusterResponse
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the possibly empty collection of top-level recursive clusters.</summary>
    [JsonPropertyName("clusters")]
    public IReadOnlyList<ClusterNode> Clusters { get; init; } = Array.Empty<ClusterNode>();

    #endregion
}
