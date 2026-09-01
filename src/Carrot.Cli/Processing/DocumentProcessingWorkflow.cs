using System.Text.Json;
using Carrot.Cli.CarrotApi;
using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Carrot.Cli.Reporting;

namespace Carrot.Cli.Processing;

/**************************************************************/
/// <summary>
/// Coordinates noninteractive document preparation and request-preview persistence.
/// </summary>
/// <remarks>
/// The active preview branch composes existing preparation, clustering-configuration, API, request-factory,
/// and atomic-persistence boundaries. It validates only through <c>/list</c> and deliberately never calls
/// <c>/cluster</c>. Full processing remains a later workflow milestone.
/// </remarks>
/// <seealso cref="IDocumentProcessingWorkflow"/>
/// <seealso cref="IDocumentPreparationWorkflow"/>
/// <seealso cref="ClusterRequestFactory"/>
internal sealed class DocumentProcessingWorkflow : IDocumentProcessingWorkflow
{
    #region implementation

    private const string RequestArtifactSuffix = ".request.json";

    private readonly IDocumentPreparationWorkflow _preparationWorkflow;
    private readonly ClusteringConfigurationResolver _configurationResolver;
    private readonly ClusteringConfigurationValidator _configurationValidator;
    private readonly ICarrotApiClient _apiClient;
    private readonly ClusterRequestFactory _requestFactory;
    private readonly IJsonArtifactWriter _jsonWriter;

    /**************************************************************/
    /// <summary>
    /// Initializes named request preview with preparation, configuration, HTTP, request, and persistence boundaries.
    /// </summary>
    /// <param name="preparationWorkflow">The shared ordered input discovery and extraction workflow.</param>
    /// <param name="configurationResolver">The clustering defaults and parameter-file resolver.</param>
    /// <param name="configurationValidator">The exact server-advertised selection validator.</param>
    /// <param name="apiClient">The Carrot list and cluster HTTP boundary.</param>
    /// <param name="requestFactory">The shared request-contract factory.</param>
    /// <param name="jsonWriter">The atomic JSON persistence boundary.</param>
    /// <exception cref="ArgumentNullException">Thrown when a required dependency is null.</exception>
    public DocumentProcessingWorkflow(
        IDocumentPreparationWorkflow preparationWorkflow,
        ClusteringConfigurationResolver configurationResolver,
        ClusteringConfigurationValidator configurationValidator,
        ICarrotApiClient apiClient,
        ClusterRequestFactory requestFactory,
        IJsonArtifactWriter jsonWriter)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(preparationWorkflow);
        ArgumentNullException.ThrowIfNull(configurationResolver);
        ArgumentNullException.ThrowIfNull(configurationValidator);
        ArgumentNullException.ThrowIfNull(apiClient);
        ArgumentNullException.ThrowIfNull(requestFactory);
        ArgumentNullException.ThrowIfNull(jsonWriter);
        _preparationWorkflow = preparationWorkflow;
        _configurationResolver = configurationResolver;
        _configurationValidator = configurationValidator;
        _apiClient = apiClient;
        _requestFactory = requestFactory;
        _jsonWriter = jsonWriter;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Reserves complete clustering and reporting orchestration for the later named-process milestone.
    /// </summary>
    /// <exception cref="NotImplementedException">Always thrown until named process orchestration is implemented.</exception>
    public Task<ProcessRunResult> ProcessAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Prepares documents, validates a request selection through <c>/list</c>, and atomically writes one request JSON artifact.
    /// </summary>
    /// <remarks>
    /// File-level preparation failures can still produce an artifact for successfully prepared documents and
    /// therefore return partial success. Configuration failures, unavailable selections, list failures, and
    /// write failures stop before any cluster submission; this method never invokes <see cref="ICarrotApiClient.ClusterAsync"/>.
    /// </remarks>
    /// <param name="request">The fully resolved noninteractive preview request.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>A complete, partial, failure, or cancellation result with stable preview exit codes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    public async Task<ProcessRunResult> PreviewAsync(PreviewRequest request, CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(request);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var preparationResult = await _preparationWorkflow.PrepareAsync(
                new PrepareDocumentsRequest { InputPaths = [request.InputPath], Recursive = request.Recursive },
                cancellationToken).ConfigureAwait(false);
            var messages = preparationResult.Messages.ToList();
            if (preparationResult.Value is not { } preparedBatch || preparedBatch.Documents.Count == 0)
            {
                return createResult(OperationStatus.Failure, ExitCodes.InputFailure, messages);
            }

            var resolvedResult = await _configurationResolver
                .ResolveAsync(request.Clustering, cancellationToken)
                .ConfigureAwait(false);
            if (resolvedResult.Value is not { } resolvedConfiguration)
            {
                messages.AddRange(resolvedResult.Messages);
                return createResult(OperationStatus.Failure, ExitCodes.InvalidConfiguration, messages);
            }

            var configurationResult = await _apiClient.GetConfigurationAsync(
                request.Endpoint,
                request.Timeout,
                indent: null,
                cancellationToken).ConfigureAwait(false);
            if (configurationResult.Value is not { } availableConfiguration)
            {
                messages.AddRange(configurationResult.Messages);
                return createResult(OperationStatus.Failure, ExitCodes.EndpointFailure, messages);
            }

            var validationResult = _configurationValidator.Validate(resolvedConfiguration, availableConfiguration);
            if (validationResult.Status == OperationStatus.Failure)
            {
                messages.AddRange(validationResult.Messages);
                return createResult(OperationStatus.Failure, ExitCodes.InvalidConfiguration, messages);
            }

            var clusterRequest = _requestFactory.Create(preparedBatch.Documents, resolvedConfiguration);
            var artifactPath = resolveArtifactPath(request);
            try
            {
                await _jsonWriter.WriteAsync(
                    artifactPath,
                    clusterRequest,
                    request.Overwrite,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
            {
                messages.Add(error(
                    "preview.artifact.write",
                    $"The request preview artifact could not be written: {artifactPath}"));
                return createResult(OperationStatus.Failure, ExitCodes.OutputFailure, messages);
            }

            return preparationResult.Status == OperationStatus.PartialSuccess
                ? createResult(OperationStatus.PartialSuccess, ExitCodes.PartialSuccess, messages, [artifactPath])
                : createResult(OperationStatus.Success, ExitCodes.Success, messages, [artifactPath]);
        }
        catch (OperationCanceledException)
        {
            return createResult(OperationStatus.Failure, ExitCodes.Cancellation, Array.Empty<OperationMessage>());
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Resolves the caller's file-or-directory output selection into one deterministic request artifact path.
    /// </summary>
    /// <remarks>
    /// An existing directory receives an input-derived <c>.request.json</c> name. An omitted output uses the
    /// input's existing parent directory. A supplied file path is preserved exactly so explicit automation paths
    /// remain stable; the atomic writer enforces overwrite and destination-parent rules.
    /// </remarks>
    /// <param name="request">The resolved preview request containing input and optional output paths.</param>
    /// <returns>The absolute request-artifact destination.</returns>
    private static string resolveArtifactPath(PreviewRequest request)
    {
        #region implementation

        var inputPath = request.InputPath;
        var inputIsDirectory = Directory.Exists(inputPath);
        var inputName = inputIsDirectory
            ? Path.GetFileName(Path.TrimEndingDirectorySeparator(inputPath))
            : Path.GetFileNameWithoutExtension(inputPath);
        if (string.IsNullOrWhiteSpace(inputName))
        {
            inputName = "carrot-preview";
        }

        var fileName = $"{inputName}{RequestArtifactSuffix}";
        if (request.OutputPath is null)
        {
            var parentDirectory = inputIsDirectory
                ? Directory.GetParent(inputPath)?.FullName ?? inputPath
                : Path.GetDirectoryName(inputPath) ?? Directory.GetCurrentDirectory();
            return Path.Combine(parentDirectory, fileName);
        }

        return Directory.Exists(request.OutputPath)
            ? Path.Combine(request.OutputPath, fileName)
            : request.OutputPath;

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one structured preview run result.</summary>
    /// <param name="status">The terminal operation status.</param>
    /// <param name="exitCode">The documented preview exit code.</param>
    /// <param name="messages">The ordered safe diagnostics collected before completion.</param>
    /// <param name="artifactPaths">The artifacts successfully created by the preview.</param>
    /// <returns>The immutable terminal run result.</returns>
    private static ProcessRunResult createResult(
        OperationStatus status,
        int exitCode,
        IReadOnlyList<OperationMessage> messages,
        IReadOnlyList<string>? artifactPaths = null)
    {
        #region implementation

        return new ProcessRunResult
        {
            RunId = Guid.NewGuid(),
            Status = status,
            ExitCode = exitCode,
            Messages = Array.AsReadOnly(messages.ToArray()),
            ArtifactPaths = artifactPaths is null ? Array.Empty<string>() : Array.AsReadOnly(artifactPaths.ToArray())
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one safe preview artifact error.</summary>
    /// <param name="code">The stable diagnostic code.</param>
    /// <param name="message">The safe user-facing error text.</param>
    /// <returns>The immutable error message.</returns>
    private static OperationMessage error(string code, string message)
    {
        #region implementation

        return new OperationMessage { Code = code, Message = message, Severity = OperationMessageSeverity.Error };

        #endregion
    }

    #endregion
}
