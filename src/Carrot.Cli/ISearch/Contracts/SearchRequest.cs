using System.Text.Json.Serialization;

namespace Carrot.Cli.ISearch.Contracts;

/**************************************************************/
/// <summary>Represents the stable bounded query context submitted to iSearch search.</summary>
/// <remarks>
/// The API boundary requires a nonempty dataset and query, an <c>AND</c> or <c>OR</c> default
/// operator, at least one result field, and a row count between 1 and 100 before it creates a
/// request. Optional advanced controls correspond to the documented iSearch search package and are
/// reused unchanged when a caller requests a later service page with a cursor.
/// </remarks>
/// <seealso cref="IISearchApiClient"/>
internal sealed class SearchRequest
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets or sets the exact live dataset name selected by the operator.</summary>
    /// <remarks>The value must come from the current <c>GET /datasets</c> response.</remarks>
    [JsonPropertyName("dataset")]
    public string Dataset { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>Gets or sets the free-text or Lucene query submitted to iSearch.</summary>
    /// <remarks>The advanced builder uses <c>*:*</c> when the operator wants filters without a base term.</remarks>
    [JsonPropertyName("q")]
    public string Query { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>Gets or sets the ordered iSearch result fields selected by the operator.</summary>
    /// <remarks>The values are serialized as the comma-separated <c>fl</c> query parameter.</remarks>
    [JsonPropertyName("fl")]
    public IReadOnlyList<string> Fields { get; set; } = Array.Empty<string>();

    /**************************************************************/
    /// <summary>Gets or sets the default Boolean operator used by the search service.</summary>
    /// <remarks>The interactive workflows default to <c>AND</c>, while advanced mode permits <c>OR</c>.</remarks>
    [JsonPropertyName("defaultOp")]
    public string DefaultOp { get; set; } = "AND";

    /**************************************************************/
    /// <summary>Gets or sets the maximum number of records requested from iSearch.</summary>
    /// <remarks>The client rejects values greater than 100 to keep one interactive request bounded.</remarks>
    [JsonPropertyName("rows")]
    public int Rows { get; set; } = 100;

    /**************************************************************/
    /// <summary>Gets or sets the optional fields used for unqualified query terms.</summary>
    /// <remarks>When omitted, iSearch uses its default query fields for the selected database.</remarks>
    [JsonPropertyName("qf")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? QueryFields { get; set; }

    /**************************************************************/
    /// <summary>Gets or sets the optional field-qualified filter expressions.</summary>
    /// <remarks>Each expression is sent as one logical <c>fq</c> value and remains in operator order.</remarks>
    [JsonPropertyName("fq")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? FilterQueries { get; set; }

    /**************************************************************/
    /// <summary>Gets or sets the optional upper update-date bound in <c>yyyy-MM-dd</c> form.</summary>
    /// <remarks>The value maps directly to iSearch's <c>updatedBefore</c> request control.</remarks>
    [JsonPropertyName("updatedBefore")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UpdatedBefore { get; set; }

    /**************************************************************/
    /// <summary>Gets or sets the optional lower update-date bound in <c>yyyy-MM-dd</c> form.</summary>
    /// <remarks>The value maps directly to iSearch's <c>updatedAfter</c> request control.</remarks>
    [JsonPropertyName("updatedAfter")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UpdatedAfter { get; set; }

    #endregion
}
