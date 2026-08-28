using System.Text.Json;
using System.Text.Json.Serialization;

namespace Carrot.Cli.CarrotApi.Contracts;

/**************************************************************/
/// <summary>
/// Models one Carrot document as the common title/content fields plus arbitrary API-defined fields.
/// </summary>
/// <remarks>
/// The Carrot OpenAPI schema declares document values as strings, while its own example also
/// demonstrates a string array. <see cref="JsonElement"/> preserves both documented shapes and
/// any future JSON-compatible field value without placing client correlation metadata on the wire.
/// </remarks>
internal sealed record ClusterDocument
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Gets the optional source-derived title field used by the planned Carrot CLI workflow.
    /// </summary>
    /// <remarks>
    /// The general Carrot document schema does not require this field; arbitrary API clients may
    /// submit differently named fields through <see cref="AdditionalFields"/> instead.
    /// </remarks>
    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }

    /**************************************************************/
    /// <summary>
    /// Gets the optional complete searchable content field used by the planned Carrot CLI workflow.
    /// </summary>
    /// <remarks>
    /// The workflow will populate this field with full extracted text. It remains optional at the
    /// wire-contract layer because the OpenAPI document permits any collection of document fields.
    /// </remarks>
    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Content { get; init; }

    /**************************************************************/
    /// <summary>
    /// Gets arbitrary additional document fields emitted as peer JSON properties.
    /// </summary>
    /// <remarks>
    /// Extension data supports examples such as <c>field1: "value1"</c> and
    /// <c>field3: ["value 1", "value 2"]</c> without introducing a wrapper property.
    /// </remarks>
    /// <seealso cref="JsonExtensionDataAttribute"/>
    [JsonExtensionData]
    public IDictionary<string, JsonElement> AdditionalFields { get; init; }
        = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

    #endregion
}
