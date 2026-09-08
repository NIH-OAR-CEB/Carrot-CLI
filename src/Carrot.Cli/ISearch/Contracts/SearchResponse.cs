using System.Text.Json;
using System.Text.Json.Serialization;

namespace Carrot.Cli.ISearch.Contracts;

/**************************************************************/
/// <summary>Represents the documented iSearch search response envelope.</summary>
/// <remarks>
/// Result records remain generic JSON because dataset schemas are discovered at runtime. The API
/// boundary accepts the response only when the returned count matches the number of records.
/// </remarks>
/// <seealso cref="IISearchApiClient"/>
internal sealed class SearchResponse
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets or sets the optional cursor supplied for later paging.</summary>
    /// <remarks>The current interactive phase displays one response and reserves this value for future paging.</remarks>
    [JsonPropertyName("cursor")]
    public string? Cursor { get; set; }

    /**************************************************************/
    /// <summary>Gets or sets the number of records returned in this response.</summary>
    /// <remarks>This value is checked against <see cref="Results"/> by the HTTP boundary.</remarks>
    [JsonPropertyName("returnedCount")]
    public int ReturnedCount { get; set; }

    /**************************************************************/
    /// <summary>Gets or sets the total number of matching records.</summary>
    /// <remarks>This value may exceed the bounded number of records included in <see cref="Results"/>.</remarks>
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    /**************************************************************/
    /// <summary>Gets or sets the generic JSON records returned for the bounded request.</summary>
    /// <remarks>Unknown dataset fields are preserved without inventing a database-specific model.</remarks>
    [JsonPropertyName("results")]
    public IReadOnlyList<JsonElement> Results { get; set; } = Array.Empty<JsonElement>();

    #endregion
}
