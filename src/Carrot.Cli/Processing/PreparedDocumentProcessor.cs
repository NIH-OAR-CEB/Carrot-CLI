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
    private readonly ClusteringConfigurationResolver _configurationResolver;
    private readonly ClusteringConfigurationValidator _configurationValidator;
    private readonly ICarrotApiClient _apiClient;
    private readonly ClusterMembershipMapper _membershipMapper;
    private readonly IRunIdProvider _runIdProvider;

    /**************************************************************/
    /// <summary>Initializes prepared-item processing with its request, HTTP, and mapping boundaries.</summary>
    /// <param name="requestFactory">The shared preview and submission request factory.</param>
    /// <param name="configurationResolver">The selection-default and parameter-file resolver.</param>
    /// <param name="configurationValidator">The exact Carrot list-response selection validator.</param>
    /// <param name="apiClient">The host-managed Carrot HTTP boundary.</param>
    /// <param name="membershipMapper">The recursive membership validator and mapper.</param>
    /// <param name="runIdProvider">The successful-run correlation identifier provider.</param>
    public PreparedDocumentProcessor(
        ClusterRequestFactory requestFactory,
        ClusteringConfigurationResolver configurationResolver,
        ClusteringConfigurationValidator configurationValidator,
        ICarrotApiClient apiClient,
        ClusterMembershipMapper membershipMapper,
        IRunIdProvider runIdProvider)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(requestFactory);
        ArgumentNullException.ThrowIfNull(configurationResolver);
        ArgumentNullException.ThrowIfNull(configurationValidator);
        ArgumentNullException.ThrowIfNull(apiClient);
        ArgumentNullException.ThrowIfNull(membershipMapper);
        ArgumentNullException.ThrowIfNull(runIdProvider);
        _requestFactory = requestFactory;
        _configurationResolver = configurationResolver;
        _configurationValidator = configurationValidator;
        _apiClient = apiClient;
        _membershipMapper = membershipMapper;
        _runIdProvider = runIdProvider;

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
            return failure("processing.no-ready-documents", "No successfully prepared documents are available to process.");
        }

        var resolvedResult = await _configurationResolver.ResolveAsync(
            request.Clustering,
            cancellationToken).ConfigureAwait(false);
        if (resolvedResult.Value is not { } resolvedConfiguration)
        {
            return OperationResult<ProcessedDocumentBatch>.Failure(resolvedResult.Messages);
        }

        var clusterRequest = _requestFactory.Create(request.PreparedBatch.Documents, resolvedConfiguration);
        var configurationResult = await _apiClient.GetConfigurationAsync(
            request.Endpoint,
            request.Timeout,
            indent: null,
            cancellationToken).ConfigureAwait(false);
        if (configurationResult.Value is not { } configuration)
        {
            return OperationResult<ProcessedDocumentBatch>.Failure(configurationResult.Messages);
        }

        var validationResult = _configurationValidator.Validate(resolvedConfiguration, configuration);
        if (validationResult.Status == OperationStatus.Failure)
        {
            return OperationResult<ProcessedDocumentBatch>.Failure(validationResult.Messages);
        }

        var clusterResult = await _apiClient.ClusterAsync(
            request.Endpoint,
            clusterRequest,
            resolvedConfiguration.Template,
            request.Timeout,
            indent: null,
            cancellationToken).ConfigureAwait(false);
        if (clusterResult.Value is not { } response)
        {
            return OperationResult<ProcessedDocumentBatch>.Failure(clusterResult.Messages);
        }

        var membershipResult = _membershipMapper.Map(response, clusterRequest.Documents.Count);
        if (membershipResult.Value is not { } memberships)
        {
            return OperationResult<ProcessedDocumentBatch>.Failure(membershipResult.Messages);
        }

        var membershipsByDocument = memberships
            .GroupBy(membership => membership.CarrotDocumentIndex)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<Reporting.ClusterMembership>)Array.AsReadOnly(group.ToArray()));
        var rows = request.PreparedBatch.Documents
            .Select((document, index) => new ProcessedDocumentRow
            {
                CarrotDocumentIndex = index,
                PreparedDocument = document,
                Memberships = membershipsByDocument.TryGetValue(index, out var documentMemberships)
                    ? documentMemberships
                    : Array.Empty<Reporting.ClusterMembership>()
            })
            .ToArray();

        return OperationResult<ProcessedDocumentBatch>.Success(new ProcessedDocumentBatch
        {
            RunId = _runIdProvider.Create(),
            Endpoint = request.Endpoint,
            Request = clusterRequest,
            Response = response,
            Rows = Array.AsReadOnly(rows)
        });

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one structured prepared-processing failure.</summary>
    /// <param name="code">The stable processing error code.</param>
    /// <param name="message">The safe user-facing failure message.</param>
    /// <returns>A failed processed-batch result.</returns>
    private static OperationResult<ProcessedDocumentBatch> failure(string code, string message)
    {
        #region implementation

        return OperationResult<ProcessedDocumentBatch>.Failure(
            [new OperationMessage { Code = code, Message = message, Severity = OperationMessageSeverity.Error }]);

        #endregion
    }

    #endregion
}
