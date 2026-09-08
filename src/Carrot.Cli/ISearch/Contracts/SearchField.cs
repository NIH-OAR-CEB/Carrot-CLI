using System.Text.Json.Serialization;
using Carrot.Cli.Cli.UI;

namespace Carrot.Cli.ISearch.Contracts;

/**************************************************************/
/// <summary>Represents one field definition returned by an iSearch dataset.</summary>
/// <remarks>
/// iSearch owns the field schema, so the contract preserves the service's field type string and
/// leaves optional metadata empty when a response omits it. The interactive fields pager displays
/// every property in the same order as the supported field-discovery script.
/// </remarks>
/// <seealso cref="IISearchApiClient"/>
/// <seealso cref="SearchFieldsPager"/>
internal sealed class SearchField
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets or sets the required service-owned field name.</summary>
    /// <remarks>The API boundary rejects a field item whose name is missing or blank.</remarks>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>Gets or sets the optional display label supplied by iSearch.</summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    /**************************************************************/
    /// <summary>Gets or sets the service-returned field type without normalization.</summary>
    /// <remarks>Values such as <c>string</c>, <c>html</c>, and <c>date</c> remain exactly as returned.</remarks>
    [JsonPropertyName("fieldType")]
    public string? FieldType { get; set; }

    /**************************************************************/
    /// <summary>Gets or sets whether iSearch uses this field as a default query field.</summary>
    /// <remarks><see langword="null"/> means the service omitted the optional flag.</remarks>
    [JsonPropertyName("defaultQueryField")]
    public bool? DefaultQueryField { get; set; }

    /**************************************************************/
    /// <summary>Gets or sets whether iSearch uses this field as a default result field.</summary>
    /// <remarks><see langword="null"/> means the service omitted the optional flag.</remarks>
    [JsonPropertyName("defaultResultField")]
    public bool? DefaultResultField { get; set; }

    /**************************************************************/
    /// <summary>Gets or sets whether the field can contain multiple values.</summary>
    /// <remarks><see langword="null"/> means the service omitted the optional flag.</remarks>
    [JsonPropertyName("multiValued")]
    public bool? MultiValued { get; set; }

    /**************************************************************/
    /// <summary>Gets or sets whether the field is restricted to search operations.</summary>
    /// <remarks><see langword="null"/> means the service omitted the optional flag.</remarks>
    [JsonPropertyName("searchOnly")]
    public bool? SearchOnly { get; set; }

    #endregion
}
