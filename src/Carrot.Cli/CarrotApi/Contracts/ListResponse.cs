using System.Text.Json;
using System.Text.Json.Serialization;

namespace Carrot.Cli.CarrotApi.Contracts;

/**************************************************************/
/// <summary>
/// Models the required algorithm/language and template maps returned by Carrot <c>/list</c>.
/// </summary>
internal sealed record ListResponse
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets each algorithm identifier and its supported language identifiers.</summary>
    [JsonPropertyName("algorithms")]
    public required IReadOnlyDictionary<string, IReadOnlyList<string>> Algorithms { get; init; }
        = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

    /**************************************************************/
    /// <summary>Gets each named template and its arbitrary template body.</summary>
    [JsonPropertyName("templates")]
    public required IReadOnlyDictionary<string, JsonElement> Templates { get; init; }
        = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

    #endregion
}
