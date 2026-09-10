using Carrot.Cli.CarrotApi.Contracts;
using Carrot.Cli.Configuration;
using Carrot.Cli.Reporting;

namespace Carrot.Cli.ISearch;

/**************************************************************/
/// <summary>Retains a successful Carrot categorization of the loaded iSearch result set.</summary>
/// <remarks>
/// This source-specific result avoids file metadata while retaining the exact request, response,
/// effective configuration, loaded-record order, and category memberships needed for display/export.
/// </remarks>
/// <seealso cref="CategorizedISearchResultRow"/>
/// <seealso cref="SearchResultPageSession"/>
internal sealed record CategorizedISearchResultBatch
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the successful Carrot run correlation identifier.</summary>
    public Guid RunId { get; init; }

    /**************************************************************/
    /// <summary>Gets the normalized Carrot endpoint used for categorization.</summary>
    public required Uri Endpoint { get; init; }

    /**************************************************************/
    /// <summary>Gets the exact four-field request submitted to Carrot.</summary>
    public required ClusterRequest Request { get; init; }

    /**************************************************************/
    /// <summary>Gets the effective clustering configuration used by Carrot.</summary>
    public required ClusteringConfiguration Configuration { get; init; }

    /**************************************************************/
    /// <summary>Gets the exact successful Carrot response.</summary>
    public required ClusterResponse Response { get; init; }

    /**************************************************************/
    /// <summary>Gets categorized iSearch rows in loaded service-page/result order.</summary>
    public IReadOnlyList<CategorizedISearchResultRow> Rows { get; init; }
        = Array.Empty<CategorizedISearchResultRow>();

    /**************************************************************/
    /// <summary>Gets the number of assigned loaded records.</summary>
    public int AssignedCount => Rows.Count(row => row.IsAssigned);

    /**************************************************************/
    /// <summary>Gets the number of loaded records without a category.</summary>
    public int UnassignedCount => Rows.Count - AssignedCount;

    /**************************************************************/
    /// <summary>Gets the total number of category memberships across loaded records.</summary>
    public int MembershipCount => Rows.Sum(row => row.Memberships.Count);

    #endregion
}
