using Carrot.Cli.Extraction;
using Carrot.Cli.Reporting;

namespace Carrot.Cli.Processing;

/**************************************************************/
/// <summary>Correlates one exact submitted document with every server-returned cluster membership.</summary>
/// <remarks>
/// An empty membership collection is an explicit unassigned outcome, not a processing failure.
/// Membership scores are retained exactly as returned and are relative only within one response.
/// </remarks>
/// <seealso cref="ProcessedDocumentBatch"/>
/// <seealso cref="ClusterMembership"/>
internal sealed record ProcessedDocumentRow
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the zero-based position in the exact submitted request document array.</summary>
    public int CarrotDocumentIndex { get; init; }

    /**************************************************************/
    /// <summary>Gets the retained prepared document referenced by that submitted position.</summary>
    public required ExtractedDocument PreparedDocument { get; init; }

    /**************************************************************/
    /// <summary>Gets all overlapping and nested memberships in depth-first server order.</summary>
    public IReadOnlyList<ClusterMembership> Memberships { get; init; } = Array.Empty<ClusterMembership>();

    /**************************************************************/
    /// <summary>Gets whether the document appears in at least one returned cluster node.</summary>
    public bool IsAssigned => Memberships.Count > 0;

    #endregion
}
