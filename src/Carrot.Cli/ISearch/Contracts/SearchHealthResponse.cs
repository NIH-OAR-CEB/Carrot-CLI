using System.Text.Json;

namespace Carrot.Cli.ISearch.Contracts;

/**************************************************************/
/// <summary>Represents the tolerant availability data returned by iSearch health.</summary>
/// <remarks>
/// The complete JSON payload is retained for display, while <see cref="Status"/> is the only
/// field used to decide whether dataset discovery may proceed.
/// </remarks>
/// <seealso cref="IISearchApiClient"/>
internal sealed class SearchHealthResponse
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the returned service status, such as <c>UP</c>, when present.</summary>
    /// <remarks>Any value other than <c>UP</c>, including a missing value, keeps the workflow closed.</remarks>
    public string? Status { get; init; }

    /**************************************************************/
    /// <summary>Gets the complete bounded JSON payload returned by the health endpoint.</summary>
    /// <remarks>The UI renders this value as literal text and does not treat it as Spectre markup.</remarks>
    public JsonElement Payload { get; init; }

    #endregion
}
