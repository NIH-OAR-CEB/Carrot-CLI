using Carrot.Cli.Common;

namespace Carrot.Cli.Processing;

/**************************************************************/
/// <summary>Defines configuration validation, submission, and exact prepared-document correlation.</summary>
/// <seealso cref="PreparedDocumentProcessor"/>
internal interface IPreparedDocumentProcessor
{
    /**************************************************************/
    /// <summary>Validates the preview configuration, submits one complete request, and maps its response.</summary>
    /// <param name="request">The retained prepared batch, endpoint, and timeout.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>A task containing the complete processed batch or structured failure messages.</returns>
    Task<OperationResult<ProcessedDocumentBatch>> ProcessAsync(
        ProcessPreparedItemsRequest request,
        CancellationToken cancellationToken);
}
