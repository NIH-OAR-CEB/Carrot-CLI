using Carrot.Cli.Common;

namespace Carrot.Cli.Processing;

/**************************************************************/
/// <summary>Defines interactive multi-input discovery, extraction, hashing, and review preparation.</summary>
/// <seealso cref="DocumentPreparationWorkflow"/>
internal interface IDocumentPreparationWorkflow
{
    /**************************************************************/
    /// <summary>Prepares the supplied ordered inputs without contacting Carrot or writing output artifacts.</summary>
    /// <param name="request">The ordered multi-input preparation request.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>A task containing a complete, partial, or failed prepared batch.</returns>
    Task<OperationResult<PreparedDocumentBatch>> PrepareAsync(
        PrepareDocumentsRequest request,
        CancellationToken cancellationToken);
}
