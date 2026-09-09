using System.Text.Json;
using System.Text.Json.Serialization;

namespace Carrot.Cli.ISearch.Contracts;

/**************************************************************/
/// <summary>Represents the documented iSearch search response envelope.</summary>
/// <remarks>
/// Result records remain generic JSON because dataset schemas are discovered at runtime. The API
/// boundary accepts the response only when the returned count matches the number of records and
/// derives the nested cardinality contract from the service envelope, request row limit, and page context.
/// </remarks>
/// <seealso cref="IISearchApiClient"/>
internal sealed class SearchResponse
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets or sets the optional service cursor supplied for the next result page.</summary>
    /// <remarks>The cursor controls service-data continuation and is unrelated to terminal display-page navigation.</remarks>
    [JsonPropertyName("cursor")]
    public string? Cursor { get; set; }

    /**************************************************************/
    /// <summary>Gets or sets the common counts and service result-page metadata for this response.</summary>
    /// <remarks>The cursor remains separate because it is the service-owned continuation token for a later result-page request.</remarks>
    public SearchCardinality Cardinality { get; set; } = new();

    /**************************************************************/
    /// <summary>Gets or sets the generic JSON records returned for the bounded request.</summary>
    /// <remarks>Unknown dataset fields are preserved without inventing a database-specific model.</remarks>
    [JsonPropertyName("results")]
    public IReadOnlyList<JsonElement> Results { get; set; } = Array.Empty<JsonElement>();

    #endregion
}
