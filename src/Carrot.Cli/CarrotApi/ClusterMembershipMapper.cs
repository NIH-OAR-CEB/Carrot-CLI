using Carrot.Cli.CarrotApi.Contracts;
using Carrot.Cli.Common;
using Carrot.Cli.Reporting;

namespace Carrot.Cli.CarrotApi;

/**************************************************************/
/// <summary>
/// Validates response indexes and recursively flattens all nested cluster memberships.
/// </summary>
/// <remarks>
/// Labels within a node are joined with <c> | </c>, path levels with <c> &gt; </c>,
/// and every matching node is retained even when documents belong to multiple clusters.
/// </remarks>
internal sealed class ClusterMembershipMapper
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Maps a recursive response into exact per-document memberships after validating every index.
    /// </summary>
    /// <param name="response">The exact Carrot cluster response.</param>
    /// <param name="documentCount">The number of documents in the submitted request.</param>
    /// <returns>All flattened memberships or a response-contract failure.</returns>
    internal OperationResult<IReadOnlyList<ClusterMembership>> Map(ClusterResponse response, int documentCount)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(response);
        if (documentCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(documentCount), "The document count must not be negative.");
        }

        var memberships = new List<ClusterMembership>();
        var failure = mapNodes(response.Clusters, documentCount, null, 0, memberships);
        return failure is null
            ? OperationResult<IReadOnlyList<ClusterMembership>>.Success(memberships.AsReadOnly())
            : OperationResult<IReadOnlyList<ClusterMembership>>.Failure([failure]);

        #endregion
    }

    /**************************************************************/
    /// <summary>Traverses cluster nodes depth-first in server order and validates every document reference.</summary>
    /// <param name="nodes">The current server-ordered cluster collection.</param>
    /// <param name="documentCount">The exact submitted document count.</param>
    /// <param name="parentPath">The flattened parent category path, when nested.</param>
    /// <param name="depth">The zero-based nesting depth.</param>
    /// <param name="memberships">The temporary collection receiving validated memberships.</param>
    /// <returns>The first contract failure, or <see langword="null"/> when traversal succeeds.</returns>
    private static OperationMessage? mapNodes(
        IReadOnlyList<ClusterNode> nodes,
        int documentCount,
        string? parentPath,
        int depth,
        ICollection<ClusterMembership> memberships)
    {
        #region implementation

        foreach (var node in nodes)
        {
            var label = string.Join(" | ", node.Labels);
            var categoryPath = string.IsNullOrEmpty(parentPath)
                ? label
                : string.IsNullOrEmpty(label)
                    ? parentPath
                    : $"{parentPath} > {label}";
            var nodeIndexes = new HashSet<int>();

            foreach (var documentIndex in node.Documents)
            {
                if (documentIndex < 0 || documentIndex >= documentCount)
                {
                    return new OperationMessage
                    {
                        Code = "carrot.response.document-index",
                        Message = $"Carrot returned document index {documentIndex}, but the submitted request contains {documentCount:N0} document(s).",
                        Severity = OperationMessageSeverity.Error
                    };
                }

                // One node can repeat an index; retain only its first server-ordered reference.
                if (!nodeIndexes.Add(documentIndex))
                {
                    continue;
                }

                memberships.Add(new ClusterMembership
                {
                    CarrotDocumentIndex = documentIndex,
                    Labels = Array.AsReadOnly(node.Labels.ToArray()),
                    CategoryPath = categoryPath,
                    Score = node.Score,
                    Depth = depth
                });
            }

            var childFailure = mapNodes(
                node.Clusters,
                documentCount,
                categoryPath,
                depth + 1,
                memberships);
            if (childFailure is not null)
            {
                return childFailure;
            }
        }

        return null;

        #endregion
    }

    #endregion
}
