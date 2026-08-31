using Carrot.Cli.CarrotApi;
using Carrot.Cli.CarrotApi.Contracts;
using Carrot.Cli.Common;
using Carrot.Cli.Reporting;
using Xunit;

namespace Carrot.Cli.Tests.CarrotApi;

/**************************************************************/
/// <summary>Verifies complete recursive, overlapping, duplicate, empty, and invalid membership mapping.</summary>
public sealed class ClusterMembershipMapperTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Verifies the supplied two-document example maps each index to its exact membership.</summary>
    [Fact]
    public void Map_SuppliedZeroAndOneExample_PreservesDocumentIndexes()
    {
        #region implementation

        // Arrange
        var response = responseWith(
            node(["First"], [0], 12.5D),
            node(["Second"], [1], 9D));

        // Act
        var result = new ClusterMembershipMapper().Map(response, 2);

        // Assert
        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Collection(
            result.Value!,
            membership =>
            {
                Assert.Equal(0, membership.CarrotDocumentIndex);
                Assert.Equal("First", membership.CategoryPath);
            },
            membership =>
            {
                Assert.Equal(1, membership.CarrotDocumentIndex);
                Assert.Equal("Second", membership.CategoryPath);
            });

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies out-of-order references, overlap, labels, nesting, duplicates, and exact scores.</summary>
    [Fact]
    public void Map_RecursiveOverlappingResponse_FlattensDepthFirstInServerOrder()
    {
        #region implementation

        // Arrange
        var nested = node(["Nested", "Detail"], [1, 1, 2], 0.12345678901234566D);
        var response = responseWith(
            node(["Root", "Topic"], [2, 0, 2], 100.25D, nested),
            node(["Overlap"], [0], score: null));

        // Act
        var result = new ClusterMembershipMapper().Map(response, 3);

        // Assert
        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Collection(
            result.Value!,
            membership => assertMembership(membership, 2, "Root | Topic", 100.25D, 0),
            membership => assertMembership(membership, 0, "Root | Topic", 100.25D, 0),
            membership => assertMembership(membership, 1, "Root | Topic > Nested | Detail", 0.12345678901234566D, 1),
            membership => assertMembership(membership, 2, "Root | Topic > Nested | Detail", 0.12345678901234566D, 1),
            membership => assertMembership(membership, 0, "Overlap", null, 0));
        Assert.Equal(["Nested", "Detail"], result.Value![2].Labels);

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies an empty cluster array is a valid successful response with no memberships.</summary>
    [Fact]
    public void Map_EmptyClusters_ReturnsSuccessfulEmptyMemberships()
    {
        #region implementation

        // Act
        var result = new ClusterMembershipMapper().Map(new ClusterResponse(), 4);

        // Assert
        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Empty(result.Value!);

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies any negative or out-of-range reference invalidates the complete response.</summary>
    /// <param name="invalidIndex">The invalid response document index.</param>
    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void Map_InvalidDocumentIndex_ReturnsNoPartialMapping(int invalidIndex)
    {
        #region implementation

        // Arrange
        var response = responseWith(
            node(["Valid first"], [0], 2D),
            node(["Invalid later"], [invalidIndex], 1D));

        // Act
        var result = new ClusterMembershipMapper().Map(response, 2);

        // Assert
        Assert.Equal(OperationStatus.Failure, result.Status);
        Assert.Null(result.Value);
        var message = Assert.Single(result.Messages);
        Assert.Equal("carrot.response.document-index", message.Code);
        Assert.Contains(invalidIndex.ToString(), message.Message, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a response containing the supplied top-level nodes.</summary>
    /// <param name="nodes">The top-level server-ordered nodes.</param>
    /// <returns>The response fixture.</returns>
    private static ClusterResponse responseWith(params ClusterNode[] nodes)
    {
        #region implementation

        return new ClusterResponse { Clusters = Array.AsReadOnly(nodes) };

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one recursive cluster-node fixture.</summary>
    /// <param name="labels">The node labels.</param>
    /// <param name="documents">The node document references.</param>
    /// <param name="score">The exact optional score.</param>
    /// <param name="children">The nested nodes.</param>
    /// <returns>The cluster-node fixture.</returns>
    private static ClusterNode node(
        string[] labels,
        int[] documents,
        double? score,
        params ClusterNode[] children)
    {
        #region implementation

        return new ClusterNode
        {
            Labels = Array.AsReadOnly(labels),
            Documents = Array.AsReadOnly(documents),
            Score = score,
            Clusters = Array.AsReadOnly(children)
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>Asserts every exact scalar value on one mapped membership.</summary>
    /// <param name="membership">The membership under test.</param>
    /// <param name="index">The expected submitted index.</param>
    /// <param name="path">The expected flattened category path.</param>
    /// <param name="score">The expected unrounded score.</param>
    /// <param name="depth">The expected nesting depth.</param>
    private static void assertMembership(
        ClusterMembership membership,
        int index,
        string path,
        double? score,
        int depth)
    {
        #region implementation

        Assert.Equal(index, membership.CarrotDocumentIndex);
        Assert.Equal(path, membership.CategoryPath);
        Assert.Equal(score, membership.Score);
        Assert.Equal(depth, membership.Depth);

        #endregion
    }

    #endregion
}
