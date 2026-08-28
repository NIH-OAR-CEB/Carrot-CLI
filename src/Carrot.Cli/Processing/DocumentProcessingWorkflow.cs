using Carrot.Cli.CarrotApi;
using Carrot.Cli.Extraction;
using Carrot.Cli.Input;
using Carrot.Cli.Reporting;

namespace Carrot.Cli.Processing;

/**************************************************************/
/// <summary>
/// Coordinates the ordered document-to-cluster-to-report application workflow.
/// </summary>
/// <remarks>
/// Future processing will preserve one global clustering request because splitting input
/// would change clustering semantics. Expected per-file failures will produce partial success.
/// </remarks>
/// <seealso cref="IDocumentProcessingWorkflow"/>
internal sealed class DocumentProcessingWorkflow : IDocumentProcessingWorkflow
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Initializes the workflow with each volatile feature boundary.
    /// </summary>
    /// <param name="inputResolver">The folder-or-archive input strategy resolver.</param>
    /// <param name="extractionCoordinator">The bounded extraction coordinator.</param>
    /// <param name="apiClient">The Carrot list and cluster client.</param>
    /// <param name="membershipMapper">The recursive response membership mapper.</param>
    /// <param name="excelWriter">The workbook reporting boundary.</param>
    /// <param name="jsonWriter">The JSON artifact persistence boundary.</param>
    /// <param name="timeProvider">The injectable run timestamp source.</param>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    internal DocumentProcessingWorkflow(
        InputSourceResolver inputResolver,
        DocumentExtractionCoordinator extractionCoordinator,
        ICarrotApiClient apiClient,
        ClusterMembershipMapper membershipMapper,
        IExcelReportWriter excelWriter,
        IJsonArtifactWriter jsonWriter,
        TimeProvider timeProvider)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Coordinates one complete clustering and reporting run.
    /// </summary>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    public Task<ProcessRunResult> ProcessAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Coordinates discovery, extraction, and request preview without clustering.
    /// </summary>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    public Task<ProcessRunResult> PreviewAsync(PreviewRequest request, CancellationToken cancellationToken)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
