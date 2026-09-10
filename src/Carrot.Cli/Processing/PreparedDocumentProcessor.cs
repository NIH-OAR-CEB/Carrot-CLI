using Carrot.Cli.CarrotApi;
using Carrot.Cli.Common;
using Carrot.Cli.Configuration;

namespace Carrot.Cli.Processing;

/**************************************************************/
/// <summary>Validates and submits the exact preview request, then correlates every response membership.</summary>
/// <remarks>
/// All ready documents remain in one request because splitting or merging cluster calls would
/// change Carrot's global clustering semantics. Source ordinals are not used for correlation;
/// each server index addresses the exact prepared document array used to create the request.
/// </remarks>
/// <seealso cref="IPreparedDocumentProcessor"/>
/// <seealso cref="ClusterRequestFactory"/>
/// <seealso cref="ClusterMembershipMapper"/>
internal sealed class PreparedDocumentProcessor : IPreparedDocumentProcessor
{
    #region implementation

    private readonly ClusterRequestFactory _requestFactory;
    private readonly ICarrotCategorizer _categorizer;

    /**************************************************************/
    /// <summary>Initializes prepared-item processing with its request, HTTP, and mapping boundaries.</summary>
    /// <param name="requestFactory">The shared preview and submission request factory.</param>
    /// <param name="categorizer">The shared list-first Carrot categorization boundary.</param>
    public PreparedDocumentProcessor(
        ClusterRequestFactory requestFactory,
        ICarrotCategorizer categorizer)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(requestFactory);
        ArgumentNullException.ThrowIfNull(categorizer);
        _requestFactory = requestFactory;
        _categorizer = categorizer;

        #endregion
    }

    /**************************************************************/
    /// <summary>Validates the preview configuration, submits one complete request, and maps its response.</summary>
    /// <param name="request">The retained prepared batch, endpoint, and timeout.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>A task containing the complete processed batch or structured failure messages.</returns>
    public async Task<OperationResult<ProcessedDocumentBatch>> ProcessAsync(
        ProcessPreparedItemsRequest request,
        CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(request);
        if (request.PreparedBatch.Documents.Count == 0)
        {
            return OperationResult<ProcessedDocumentBatch>.Failure(
            [new OperationMessage
            {
                Code = "processing.no-ready-documents",
                Message = "No successfully prepared documents are available to process.",
                Severity = OperationMessageSeverity.Error
            }]);
        }

        var categorization = await _categorizer.CategorizeAsync(
            new CarrotCategorizationRequest
            {
                Endpoint = request.Endpoint,
                Documents = _requestFactory.CreateDocuments(request.PreparedBatch.Documents),
                Clustering = request.Clustering,
                Timeout = request.Timeout
            },
            cancellationToken).ConfigureAwait(false);
        if (categorization.Value is not { } categoryResult)
        {
            return OperationResult<ProcessedDocumentBatch>.Failure(categorization.Messages);
        }

        var rows = request.PreparedBatch.Documents
            .Select((document, index) => new ProcessedDocumentRow
            {
                CarrotDocumentIndex = index,
                PreparedDocument = document,
                Memberships = categoryResult.MembershipsByDocument[index]
            })
            .ToArray();

        return OperationResult<ProcessedDocumentBatch>.Success(new ProcessedDocumentBatch
        {
            RunId = categoryResult.RunId,
            Endpoint = request.Endpoint,
            Request = categoryResult.Request,
            Template = categoryResult.Configuration.Template,
            Response = categoryResult.Response,
            Rows = Array.AsReadOnly(rows)
        });

        #endregion
    }

    #endregion
}
