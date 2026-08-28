using Xunit;

namespace Carrot.Cli.Tests.Input;

/**************************************************************/
/// <summary>
/// Reserves future acceptance coverage for deterministic folder and guarded ZIP discovery.
/// </summary>
public sealed class InputDiscoveryTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Verifies normalized relative-path order, recursion, temp-file exclusion, and reparse safety.</summary>
    [Fact(Skip = "Future acceptance: folder discovery is layout-only.")]
    public void FolderDiscoveryIsDeterministicAndSafe()
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies ZIP traversal, absolute paths, nested archives, limits, and compression ratios are rejected.</summary>
    [Fact(Skip = "Future acceptance: ZIP discovery is layout-only.")]
    public void ZipDiscoveryEnforcesAllArchiveGuards()
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
