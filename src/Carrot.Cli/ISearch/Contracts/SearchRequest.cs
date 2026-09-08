using System.Text.Json.Serialization;

namespace Carrot.Cli.ISearch.Contracts;

/**************************************************************/
/// <summary>Represents the bounded JSON body submitted to iSearch search.</summary>
/// <remarks>
/// The API boundary requires a nonempty database and query, the exact <c>AND</c> default operator,
/// and a row count between 1 and 100 before it creates a request.
/// </remarks>
/// <seealso cref="IISearchApiClient"/>
internal sealed class SearchRequest
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets or sets the exact live dataset name selected by the operator.</summary>
    /// <remarks>The value must come from the current <c>GET /datasets</c> response.</remarks>
    [JsonPropertyName("database")]
    public string Database { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>Gets or sets the free-text or Lucene query submitted to iSearch.</summary>
    /// <remarks>The first interactive phase does not qualify or persist the query text.</remarks>
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>Gets or sets the default Boolean operator used by the search service.</summary>
    /// <remarks>The interactive workflow always sends <c>AND</c>.</remarks>
    [JsonPropertyName("defaultOp")]
    public string DefaultOp { get; set; } = "AND";

    /**************************************************************/
    /// <summary>Gets or sets the maximum number of records requested from iSearch.</summary>
    /// <remarks>The client rejects values greater than 100 to keep one interactive request bounded.</remarks>
    [JsonPropertyName("rows")]
    public int Rows { get; set; } = 100;

    #endregion
}
