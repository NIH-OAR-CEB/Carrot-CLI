using System.Text.Encodings.Web;
using System.Text.Json;

namespace Carrot.Cli.Reporting;

/**************************************************************/
/// <summary>Formats category memberships consistently for category-oriented Excel reports.</summary>
/// <remarks>Relaxed escaping keeps readable Unicode while the Excel writer stores the result as inert text.</remarks>
/// <seealso cref="ClusterMembership"/>
internal static class CategoryMembershipJsonFormatter
{
    #region implementation

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /**************************************************************/
    /// <summary>Serializes one membership as the established singleton JSON array.</summary>
    /// <param name="membership">The membership to serialize.</param>
    /// <returns>Deterministic category-membership JSON.</returns>
    internal static string Serialize(ClusterMembership membership)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(membership);
        return JsonSerializer.Serialize(new[] { membership }, SerializerOptions);

        #endregion
    }

    #endregion
}
