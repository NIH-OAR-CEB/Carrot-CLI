using Xunit;

namespace Carrot.Cli.Tests.Cli;

/**************************************************************/
/// <summary>
/// Reserves future acceptance coverage for the complete noninteractive and interactive command surface.
/// </summary>
public sealed class CommandContractTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Verifies command names, process options, help topics, and version behavior.</summary>
    [Fact(Skip = "Future acceptance: command and option metadata are layout-only.")]
    public void CommandsExposeTheDocumentedContract()
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies no arguments launch the menu while scheduled commands never prompt.</summary>
    [Fact(Skip = "Future acceptance: interactive dispatch is layout-only.")]
    public void DispatchSeparatesInteractiveAndScheduledExecution()
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies endpoint option and environment precedence plus missing-endpoint failure.</summary>
    [Fact(Skip = "Future acceptance: endpoint resolution is layout-only.")]
    public void EndpointResolutionUsesDocumentedPrecedence()
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
