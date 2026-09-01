using System.Text.Json;
using Carrot.Cli.CarrotApi;
using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Carrot.Cli.Reporting;

namespace Carrot.Cli.Processing;

/**************************************************************/
/// <summary>
/// Coordinates noninteractive document preparation, preview persistence, and complete processing output.
/// </summary>
/// <remarks>
/// Preview validates through <c>/list</c> and deliberately never calls <c>/cluster</c>. Processing delegates the
/// single global request and membership correlation to <see cref="IPreparedDocumentProcessor"/>, then writes its
/// report and optional JSON sidecars from the same retained batch.
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
    private readonly IPreparedDocumentProcessor _preparedProcessor;
    private readonly ProcessedDocumentReportMapper _reportMapper;
    private readonly IExcelReportWriter _excelWriter;

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
    /// <param name="preparedProcessor">The shared single-request processing and correlation boundary.</param>
    /// <param name="reportMapper">The retained processed-batch to workbook-row mapper.</param>
    /// <param name="excelWriter">The atomic workbook persistence boundary.</param>
    /// <exception cref="ArgumentNullException">Thrown when a required dependency is null.</exception>
    public DocumentProcessingWorkflow(
        IDocumentPreparationWorkflow preparationWorkflow,
        ClusteringConfigurationResolver configurationResolver,
        ClusteringConfigurationValidator configurationValidator,
        ICarrotApiClient apiClient,
        ClusterRequestFactory requestFactory,
        IJsonArtifactWriter jsonWriter,
        IPreparedDocumentProcessor preparedProcessor,
        ProcessedDocumentReportMapper reportMapper,
        IExcelReportWriter excelWriter)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(preparationWorkflow);
        ArgumentNullException.ThrowIfNull(configurationResolver);
        ArgumentNullException.ThrowIfNull(configurationValidator);
        ArgumentNullException.ThrowIfNull(apiClient);
        ArgumentNullException.ThrowIfNull(requestFactory);
        ArgumentNullException.ThrowIfNull(jsonWriter);
        ArgumentNullException.ThrowIfNull(preparedProcessor);
        ArgumentNullException.ThrowIfNull(reportMapper);
        ArgumentNullException.ThrowIfNull(excelWriter);
        _preparationWorkflow = preparationWorkflow;
        _configurationResolver = configurationResolver;
        _configurationValidator = configurationValidator;
        _apiClient = apiClient;
        _requestFactory = requestFactory;
        _jsonWriter = jsonWriter;
        _preparedProcessor = preparedProcessor;
        _reportMapper = reportMapper;
        _excelWriter = excelWriter;

        #endregion
    }

    /**************************************************************/
    /// <summary>Prepares, submits, correlates, and atomically persists one named processing run.</summary>
    /// <param name="request">The resolved noninteractive process request.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>The complete, partial, failure, or cancellation process result.</returns>
    public async Task<ProcessRunResult> ProcessAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var preparation = await _preparationWorkflow.PrepareAsync(
                new PrepareDocumentsRequest { InputPaths = [request.InputPath], Recursive = request.Recursive },
                cancellationToken).ConfigureAwait(false);
            var messages = preparation.Messages.ToList();
            if (preparation.Value is not { } batch || batch.Documents.Count == 0)
            {
                return createResult(OperationStatus.Failure, ExitCodes.InputFailure, messages);
            }

            var processed = await _preparedProcessor.ProcessAsync(new ProcessPreparedItemsRequest
            {
                Endpoint = request.Endpoint,
                PreparedBatch = batch,
                Clustering = request.Clustering,
                Timeout = request.Timeout
            }, cancellationToken).ConfigureAwait(false);
            if (processed.Value is not { } processedBatch)
            {
                messages.AddRange(processed.Messages);
                return createResult(OperationStatus.Failure, classifyProcessingFailure(processed.Messages), messages);
            }

            var paths = resolveProcessArtifactPaths(request, processedBatch.RunId);
            var artifactPaths = new List<string>();
            try
            {
                if (request.WriteJsonArtifacts)
                {
                    await _jsonWriter.WriteAsync(paths.RequestPath, processedBatch.Request, request.Overwrite, cancellationToken).ConfigureAwait(false);
                    artifactPaths.Add(paths.RequestPath);
                }

                var report = _reportMapper.Create(processedBatch, paths.WorkbookPath, request.Overwrite);
                await _excelWriter.WriteAsync(report, cancellationToken).ConfigureAwait(false);
                artifactPaths.Add(paths.WorkbookPath);

                if (request.WriteJsonArtifacts)
                {
                    await _jsonWriter.WriteAsync(paths.ResponsePath, processedBatch.Response, request.Overwrite, cancellationToken).ConfigureAwait(false);
                    artifactPaths.Add(paths.ResponsePath);
                }

                return preparation.Status == OperationStatus.PartialSuccess
                    ? createResult(OperationStatus.PartialSuccess, ExitCodes.PartialSuccess, messages, artifactPaths, report.Rows, processedBatch.RunId)
                    : createResult(OperationStatus.Success, ExitCodes.Success, messages, artifactPaths, report.Rows, processedBatch.RunId);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
            {
                messages.Add(error("process.artifact.write", "One or more process artifacts could not be written."));
                return createResult(OperationStatus.Failure, ExitCodes.OutputFailure, messages, artifactPaths, runId: processedBatch.RunId);
            }
        }
        catch (OperationCanceledException)
        {
            return createResult(OperationStatus.Failure, ExitCodes.Cancellation, Array.Empty<OperationMessage>());
        }

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
    /// <summary>Derives deterministic workbook and sidecar destinations for one process run.</summary>
    private static (string WorkbookPath, string RequestPath, string ResponsePath) resolveProcessArtifactPaths(
        ProcessRequest request,
        Guid runId)
    {
        #region implementation

        var inputDirectory = Directory.Exists(request.InputPath);
        var inputName = inputDirectory
            ? Path.GetFileName(Path.TrimEndingDirectorySeparator(request.InputPath))
            : Path.GetFileNameWithoutExtension(request.InputPath);
        inputName = string.IsNullOrWhiteSpace(inputName) ? "carrot-results" : inputName;
        var directory = request.OutputPath is not null && Directory.Exists(request.OutputPath)
            ? request.OutputPath
            : request.OutputPath is null
                ? inputDirectory ? Directory.GetParent(request.InputPath)?.FullName ?? request.InputPath : Path.GetDirectoryName(request.InputPath)!
                : Path.GetDirectoryName(request.OutputPath)!;
        var workbookPath = request.OutputPath is not null && !Directory.Exists(request.OutputPath)
            ? request.OutputPath
            : Path.Combine(directory, $"{inputName}-carrot-{runId:D}.xlsx");
        var sidecarPrefix = Path.Combine(
            Path.GetDirectoryName(workbookPath)!,
            Path.GetFileNameWithoutExtension(workbookPath));
        return (workbookPath, $"{sidecarPrefix}.request.json", $"{sidecarPrefix}.response.json");

        #endregion
    }

    /**************************************************************/
    /// <summary>Maps known prepared-processing diagnostics to documented process exit codes.</summary>
    private static int classifyProcessingFailure(IReadOnlyList<OperationMessage> messages)
    {
        #region implementation

        return messages.Any(message => message.Code.StartsWith("clustering.", StringComparison.Ordinal))
            ? ExitCodes.InvalidConfiguration
            : messages.Any(message => message.Code == "processing.list.failure")
                ? ExitCodes.EndpointFailure
            : ExitCodes.ClusterFailure;

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one structured preview or process run result.</summary>
    /// <param name="status">The terminal operation status.</param>
    /// <param name="exitCode">The documented command exit code.</param>
    /// <param name="messages">The ordered safe diagnostics collected before completion.</param>
    /// <param name="artifactPaths">The successfully persisted artifact paths.</param>
    /// <param name="rows">The completed report rows when processing reached report mapping.</param>
    /// <param name="runId">The retained processing correlation identifier, or null for preview-only runs.</param>
    /// <returns>The immutable terminal run result.</returns>
    private static ProcessRunResult createResult(
        OperationStatus status,
        int exitCode,
        IReadOnlyList<OperationMessage> messages,
        IReadOnlyList<string>? artifactPaths = null,
        IReadOnlyList<ReportRow>? rows = null,
        Guid? runId = null)
    {
        #region implementation

        return new ProcessRunResult
        {
            RunId = runId ?? Guid.NewGuid(),
            Status = status,
            ExitCode = exitCode,
            Messages = Array.AsReadOnly(messages.ToArray()),
            ArtifactPaths = artifactPaths is null ? Array.Empty<string>() : Array.AsReadOnly(artifactPaths.ToArray()),
            Rows = rows is null ? Array.Empty<ReportRow>() : Array.AsReadOnly(rows.ToArray())
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
