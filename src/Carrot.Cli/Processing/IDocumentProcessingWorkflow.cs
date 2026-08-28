namespace Carrot.Cli.Processing;

/**************************************************************/
/// <summary>
/// Defines complete-processing and request-preview orchestration boundaries.
/// </summary>
/// <seealso cref="DocumentProcessingWorkflow"/>
internal interface IDocumentProcessingWorkflow
{
    /**************************************************************/
    /// <summary>
    /// Discovers, extracts, validates, clusters, maps, and reports one complete input batch.
    /// </summary>
    /// <param name="request">The fully resolved processing request.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>A task containing the terminal run result.</returns>
    Task<ProcessRunResult> ProcessAsync(ProcessRequest request, CancellationToken cancellationToken);

    /**************************************************************/
    /// <summary>
    /// Discovers and extracts documents, then writes a serialized request without submission.
    /// </summary>
    /// <param name="request">The fully resolved preview request.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>A task containing the terminal preview result.</returns>
    Task<ProcessRunResult> PreviewAsync(PreviewRequest request, CancellationToken cancellationToken);
}
