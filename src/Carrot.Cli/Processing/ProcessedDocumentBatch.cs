using Carrot.Cli.CarrotApi.Contracts;

namespace Carrot.Cli.Processing;

/**************************************************************/
/// <summary>Retains one complete successful in-memory Carrot request, response, and correlation result.</summary>
/// <remarks>
/// This value is replaced only after a later complete success. No workbook, JSON sidecar, or
/// other output artifact is written by the interactive prepared-items workflow.
/// </remarks>
/// <seealso cref="ProcessedDocumentRow"/>
internal sealed record ProcessedDocumentBatch
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the normalized endpoint that processed the batch.</summary>
    public required Uri Endpoint { get; init; }

    /**************************************************************/
    /// <summary>Gets the exact request created by the shared preview request factory.</summary>
    public required ClusterRequest Request { get; init; }

    /**************************************************************/
    /// <summary>Gets the exact successfully parsed Carrot response.</summary>
    public required ClusterResponse Response { get; init; }

    /**************************************************************/
    /// <summary>Gets one correlated row for every submitted document in request order.</summary>
    public IReadOnlyList<ProcessedDocumentRow> Rows { get; init; } = Array.Empty<ProcessedDocumentRow>();

    /**************************************************************/
    /// <summary>Gets the number of documents assigned to at least one cluster.</summary>
    public int AssignedCount => Rows.Count(row => row.IsAssigned);

    /**************************************************************/
    /// <summary>Gets the number of documents absent from every returned cluster.</summary>
    public int UnassignedCount => Rows.Count - AssignedCount;

    /**************************************************************/
    /// <summary>Gets the total number of overlapping and nested memberships across all documents.</summary>
    public int MembershipCount => Rows.Sum(row => row.Memberships.Count);

    #endregion
}
