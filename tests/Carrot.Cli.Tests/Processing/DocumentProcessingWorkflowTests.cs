using Xunit;

namespace Carrot.Cli.Tests.Processing;

/**************************************************************/
/// <summary>
/// Reserves future workflow, failure, retry, cancellation, and end-to-end acceptance coverage.
/// </summary>
public sealed class DocumentProcessingWorkflowTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Verifies payload ordering, list validation, one cluster call, and exact source correlation.</summary>
    [Fact(Skip = "Future acceptance: document workflow orchestration is layout-only.")]
    public void ProcessPreservesGlobalClusteringSemanticsAndSourceCorrelation()
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies preview writes the complete request but never submits it to the cluster endpoint.</summary>
    [Fact(Skip = "Future acceptance: preview orchestration is layout-only.")]
    public void PreviewNeverCallsTheClusterEndpoint()
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies transient retry limits, no HTTP 400 retry, cancellation, and all documented exit codes.</summary>
    [Fact(Skip = "Future acceptance: resilience and exit-code behavior are layout-only.")]
    public void WorkflowMapsFailuresRetriesAndCancellationToStableExitCodes()
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies a mixed fixture batch end to end through a fake HTTP handler without a live server.</summary>
    [Fact(Skip = "Future end-to-end acceptance: fixtures and fake HTTP behavior are layout-only.")]
    public void MixedDocumentsCompleteEndToEndWithoutALiveCarrotServer()
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
