using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Carrot.Cli.Processing;
using Microsoft.Extensions.Options;

namespace Carrot.Cli.CarrotApi;

/**************************************************************/
/// <summary>Executes the shared list-first Carrot categorization workflow.</summary>
/// <remarks>
/// This service owns only Carrot orchestration. It resolves the effective clustering settings,
/// validates them against <c>/list</c>, submits one complete <c>/cluster</c> request, maps every
/// response index, and assigns a run identifier only after complete success.
/// </remarks>
/// <seealso cref="ICarrotCategorizer"/>
/// <seealso cref="PreparedDocumentProcessor"/>
internal sealed class CarrotCategorizer : ICarrotCategorizer
{
    #region implementation

    private readonly ClusterRequestFactory _requestFactory;
    private readonly ClusteringConfigurationResolver _configurationResolver;
    private readonly ClusteringConfigurationValidator _configurationValidator;
    private readonly ICarrotApiClient _apiClient;
    private readonly ClusterMembershipMapper _membershipMapper;
    private readonly IRunIdProvider _runIdProvider;

    /**************************************************************/
    /// <summary>Initializes the shared Carrot categorization dependencies.</summary>
    /// <param name="requestFactory">The source-neutral Carrot request factory.</param>
    /// <param name="configurationResolver">The clustering selection resolver.</param>
    /// <param name="configurationValidator">The exact <c>/list</c> response validator.</param>
    /// <param name="apiClient">The Carrot HTTP boundary.</param>
    /// <param name="membershipMapper">The recursive response-index mapper.</param>
    /// <param name="runIdProvider">The successful-run identifier provider.</param>
    public CarrotCategorizer(
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
    /// <summary>Validates and submits one complete ordered document collection to Carrot.</summary>
    /// <param name="request">The endpoint, documents, clustering selection, and timeout.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>The correlated category result or a structured expected failure.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    public async Task<OperationResult<CarrotCategorizationResult>> CategorizeAsync(
        CarrotCategorizationRequest request,
        CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(request);
        if (request.Documents.Count == 0)
        {
            return failure("carrot.categorization.no-documents", "No documents are available for Carrot categorization.");
        }

        var resolvedResult = await _configurationResolver.ResolveAsync(
            request.Clustering,
            cancellationToken).ConfigureAwait(false);
        if (resolvedResult.Value is not { } resolvedConfiguration)
        {
            return OperationResult<CarrotCategorizationResult>.Failure(resolvedResult.Messages);
        }

        var clusterRequest = _requestFactory.CreateFromCarrotDocuments(request.Documents, resolvedConfiguration);
        var configurationResult = await _apiClient.GetConfigurationAsync(
            request.Endpoint,
            request.Timeout,
            indent: null,
            cancellationToken).ConfigureAwait(false);
        if (configurationResult.Value is not { } availableConfiguration)
        {
            return failureAtStage(
                "processing.list.failure",
                "The Carrot service configuration could not be retrieved.",
                configurationResult.Messages);
        }

        var validationResult = _configurationValidator.Validate(resolvedConfiguration, availableConfiguration);
        if (validationResult.Status == OperationStatus.Failure)
        {
            return OperationResult<CarrotCategorizationResult>.Failure(validationResult.Messages);
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
            return failureAtStage(
                "processing.cluster.failure",
                "The Carrot clustering request did not complete.",
                clusterResult.Messages);
        }

        var membershipResult = _membershipMapper.Map(response, clusterRequest.Documents.Count);
        if (membershipResult.Value is not { } memberships)
        {
            return OperationResult<CarrotCategorizationResult>.Failure(membershipResult.Messages);
        }

        var membershipsByDocument = Enumerable.Range(0, clusterRequest.Documents.Count)
            .Select(index => (IReadOnlyList<Reporting.ClusterMembership>)Array.AsReadOnly(
                memberships.Where(membership => membership.CarrotDocumentIndex == index).ToArray()))
            .ToArray();

        return OperationResult<CarrotCategorizationResult>.Success(new CarrotCategorizationResult
        {
            RunId = _runIdProvider.Create(),
            Endpoint = request.Endpoint,
            Request = clusterRequest,
            Configuration = resolvedConfiguration,
            Response = response,
            MembershipsByDocument = Array.AsReadOnly(membershipsByDocument)
        });

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one structured categorization failure.</summary>
    /// <param name="code">The stable failure code.</param>
    /// <param name="message">The safe operator-facing message.</param>
    /// <returns>A failed categorization result.</returns>
    private static OperationResult<CarrotCategorizationResult> failure(string code, string message)
    {
        #region implementation

        return OperationResult<CarrotCategorizationResult>.Failure(
        [new OperationMessage { Code = code, Message = message, Severity = OperationMessageSeverity.Error }]);

        #endregion
    }

    /**************************************************************/
    /// <summary>Preserves a dependency failure while adding the failed Carrot stage.</summary>
    /// <param name="code">The stage-specific failure code.</param>
    /// <param name="message">The safe stage-level message.</param>
    /// <param name="messages">The dependency messages to preserve.</param>
    /// <returns>A failed categorization result with the original diagnostics.</returns>
    private static OperationResult<CarrotCategorizationResult> failureAtStage(
        string code,
        string message,
        IReadOnlyList<OperationMessage> messages)
    {
        #region implementation

        return OperationResult<CarrotCategorizationResult>.Failure(
            messages.Concat([new OperationMessage
            {
                Code = code,
                Message = message,
                Severity = OperationMessageSeverity.Error
            }]).ToArray());

        #endregion
    }

    #endregion
}
