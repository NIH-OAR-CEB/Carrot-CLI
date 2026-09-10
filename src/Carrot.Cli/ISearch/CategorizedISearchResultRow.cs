using System.Text.Json;
using Carrot.Cli.Reporting;

namespace Carrot.Cli.ISearch;

/**************************************************************/
/// <summary>Retains one iSearch result and its correlated Carrot memberships.</summary>
/// <remarks>
/// The original four field values are kept as detached JSON values so export can preserve the
/// source representation while category memberships remain indexed by the submitted record.
/// </remarks>
/// <seealso cref="CategorizedISearchResultBatch"/>
/// <seealso cref="ClusterMembership"/>
internal sealed record CategorizedISearchResultRow
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the one-based iSearch service page containing the result.</summary>
    public int ResultPage { get; init; }

    /**************************************************************/
    /// <summary>Gets the zero-based ordinal across all loaded iSearch results.</summary>
    public int ResultOrdinal { get; init; }

    /**************************************************************/
    /// <summary>Gets the NIH application identifier value.</summary>
    public required JsonElement NihApplId { get; init; }

    /**************************************************************/
    /// <summary>Gets the result title value.</summary>
    public required JsonElement Title { get; init; }

    /**************************************************************/
    /// <summary>Gets the result abstract value.</summary>
    public required JsonElement Abstract { get; init; }

    /**************************************************************/
    /// <summary>Gets the result specific-aims value.</summary>
    public required JsonElement SpecificAims { get; init; }

    /**************************************************************/
    /// <summary>Gets all memberships for this result in Carrot depth-first response order.</summary>
    public IReadOnlyList<ClusterMembership> Memberships { get; init; } = Array.Empty<ClusterMembership>();

    /**************************************************************/
    /// <summary>Gets whether Carrot assigned this result to at least one category.</summary>
    public bool IsAssigned => Memberships.Count > 0;

    #endregion
}
