using Xunit;

namespace Carrot.Cli.Tests.CarrotApi;

/**************************************************************/
/// <summary>
/// Reserves future acceptance coverage for recursive, multi-membership response mapping.
/// </summary>
public sealed class ClusterMembershipMapperTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Verifies nested and multiple category paths, labels, scores, and unassigned documents.</summary>
    [Fact(Skip = "Future acceptance: recursive membership mapping is layout-only.")]
    public void MapperFlattensEveryExactMembership()
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies any negative or out-of-range response document index invalidates the response.</summary>
    [Fact(Skip = "Future acceptance: response index validation is layout-only.")]
    public void MapperRejectsInvalidDocumentIndexes()
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
