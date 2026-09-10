using Carrot.Cli.CarrotApi.Contracts;
using Carrot.Cli.Configuration;
using Carrot.Cli.Processing;
using Carrot.Cli.Reporting;

namespace Carrot.Cli.CarrotApi;

/**************************************************************/
/// <summary>Retains one successful source-neutral Carrot categorization and indexed memberships.</summary>
/// <remarks>
/// Membership collections are indexed by the exact document position submitted to Carrot. Source
/// adapters use that stable position to project categories back into their own result models.
/// </remarks>
/// <seealso cref="CarrotCategorizationRequest"/>
/// <seealso cref="ClusterMembership"/>
internal sealed record CarrotCategorizationResult
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the successful run correlation identifier.</summary>
    public Guid RunId { get; init; }

    /**************************************************************/
    /// <summary>Gets the normalized endpoint used for the successful operation.</summary>
    public required Uri Endpoint { get; init; }

    /**************************************************************/
    /// <summary>Gets the exact request sent to Carrot.</summary>
    public required ClusterRequest Request { get; init; }

    /**************************************************************/
    /// <summary>Gets the effective direct or template clustering configuration.</summary>
    public required ClusteringConfiguration Configuration { get; init; }

    /**************************************************************/
    /// <summary>Gets the exact successfully parsed Carrot response.</summary>
    public required ClusterResponse Response { get; init; }

    /**************************************************************/
    /// <summary>Gets memberships grouped by zero-based submitted document index.</summary>
    public IReadOnlyList<IReadOnlyList<ClusterMembership>> MembershipsByDocument { get; init; }
        = Array.Empty<IReadOnlyList<ClusterMembership>>();

    #endregion
}
