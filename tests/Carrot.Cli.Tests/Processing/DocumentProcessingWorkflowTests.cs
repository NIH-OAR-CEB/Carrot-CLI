using Xunit;

namespace Carrot.Cli.Tests.Processing;

/**************************************************************/
/// <summary>
/// Reserves future named-workflow, persistence, exit-code, and end-to-end acceptance coverage.
/// </summary>
public sealed class DocumentProcessingWorkflowTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Verifies named-workflow payload ordering, list validation, clustering, and reporting.</summary>
    [Fact(Skip = "Future acceptance: named document workflow orchestration is layout-only.")]
    public void ProcessPreservesGlobalClusteringSemanticsAndSourceCorrelation()
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies named preview writes the complete request but never submits it to the cluster endpoint.</summary>
    [Fact(Skip = "Future acceptance: named preview orchestration is layout-only.")]
    public void PreviewNeverCallsTheClusterEndpoint()
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies named workflow failures, cancellation, artifact handling, and documented exit codes.</summary>
    [Fact(Skip = "Future acceptance: named workflow exit-code and persistence behavior are layout-only.")]
    public void WorkflowMapsFailuresRetriesAndCancellationToStableExitCodes()
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies a mixed fixture batch writes planned artifacts without a live Carrot server.</summary>
    [Fact(Skip = "Future acceptance: named end-to-end persistence remains layout-only.")]
    public void MixedDocumentsCompleteEndToEndWithoutALiveCarrotServer()
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
