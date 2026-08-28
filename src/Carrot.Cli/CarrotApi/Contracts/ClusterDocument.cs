using System.Text.Json.Serialization;

namespace Carrot.Cli.CarrotApi.Contracts;

/**************************************************************/
/// <summary>
/// Models one Carrot document using only title and content so client correlation data cannot cluster.
/// </summary>
internal sealed record ClusterDocument
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the source-derived document title field.</summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /**************************************************************/
    /// <summary>Gets the complete extracted searchable content field.</summary>
    [JsonPropertyName("content")]
    public required string Content { get; init; }

    #endregion
}
