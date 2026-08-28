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
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    internal OperationResult<IReadOnlyList<ClusterMembership>> Map(ClusterResponse response, int documentCount)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
