using System.Text;
using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Carrot.Cli.Extraction;
using Carrot.Cli.Input;
using Microsoft.Extensions.Options;

namespace Carrot.Cli.Processing;

/**************************************************************/
/// <summary>
/// Coordinates ordered multi-input loading, deduplication, extraction, hashing, and review-row creation.
/// </summary>
/// <remarks>
/// Input containers are handled sequentially so temporary ZIP expansion is bounded to one archive.
/// Extraction within each container uses the coordinator's configured bounded concurrency.
/// </remarks>
/// <seealso cref="IDocumentPreparationWorkflow"/>
internal sealed class DocumentPreparationWorkflow : IDocumentPreparationWorkflow
{
    #region implementation

    private readonly InputPathNormalizer _pathNormalizer;
    private readonly InputSourceResolver _inputResolver;
    private readonly DocumentExtractionCoordinator _extractionCoordinator;
    private readonly CarrotCliOptions _options;

    /**************************************************************/
    /// <summary>Initializes preparation with path, input, extraction, and safeguard collaborators.</summary>
    public DocumentPreparationWorkflow(
        InputPathNormalizer pathNormalizer,
        InputSourceResolver inputResolver,
        DocumentExtractionCoordinator extractionCoordinator,
        IOptions<CarrotCliOptions> options)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(pathNormalizer);
        ArgumentNullException.ThrowIfNull(inputResolver);
        ArgumentNullException.ThrowIfNull(extractionCoordinator);
        ArgumentNullException.ThrowIfNull(options);
        _pathNormalizer = pathNormalizer;
        _inputResolver = inputResolver;
        _extractionCoordinator = extractionCoordinator;
        _options = options.Value;

        #endregion
    }

    /**************************************************************/
    /// <summary>Prepares all requested input containers and retains usable documents after partial failures.</summary>
    public async Task<OperationResult<PreparedDocumentBatch>> PrepareAsync(
        PrepareDocumentsRequest request,
        CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(request);
        if (request.InputPaths.Count == 0)
        {
            return OperationResult<PreparedDocumentBatch>.Failure(
                [error("preparation.input.empty", "Add at least one input path before preparing documents.")]);
        }

        var messages = new List<OperationMessage>();
        var rows = new List<PreparedDocumentRow>();
        var documents = new List<ExtractedDocument>();
        var sourceKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var nextSourceOrdinal = 0;
        long extractedCharacters = 0;
        var hadSkippedOrFailedSource = false;

        foreach (var rawInputPath in request.InputPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedPath = _pathNormalizer.Normalize(rawInputPath);
            if (normalizedPath.Status == OperationStatus.Failure || normalizedPath.Value is null)
            {
                messages.AddRange(normalizedPath.Messages);
                hadSkippedOrFailedSource = true;
                continue;
            }

            var loaderResult = _inputResolver.Resolve(normalizedPath.Value);
            if (loaderResult.Status == OperationStatus.Failure || loaderResult.Value is null)
            {
                messages.AddRange(loaderResult.Messages);
                hadSkippedOrFailedSource = true;
                continue;
            }

            var loadResult = await loaderResult.Value
                .LoadAsync(normalizedPath.Value, request.Recursive, cancellationToken)
                .ConfigureAwait(false);
            messages.AddRange(loadResult.Messages);
            hadSkippedOrFailedSource |= loadResult.Status != OperationStatus.Success;
            if (loadResult.Value is null)
            {
                continue;
            }

            var loadedBatch = loadResult.Value;
            try
            {
                var uniqueFiles = new List<SourceFile>();
                foreach (var sourceFile in loadedBatch.Files)
                {
                    if (!sourceKeys.Add(sourceFile.SourceKey))
                    {
                        messages.Add(warning(
                            "preparation.duplicate",
                            $"Skipped duplicate source: {sourceFile.ContainerPath} :: {sourceFile.RelativePath}"));
                        hadSkippedOrFailedSource = true;
                        continue;
                    }

                    if (nextSourceOrdinal >= _options.MaximumInputFiles)
                    {
                        messages.Add(error(
                            "preparation.file-limit",
                            $"The batch exceeds the {_options.MaximumInputFiles:N0}-file limit; remaining sources were skipped."));
                        hadSkippedOrFailedSource = true;
                        break;
                    }

                    uniqueFiles.Add(sourceFile with { SourceOrdinal = nextSourceOrdinal++ });
                }

                if (uniqueFiles.Count == 0)
                {
                    continue;
                }

                var extractionBatch = new InputBatch
                {
                    ContainerPath = loadedBatch.ContainerPath,
                    Files = uniqueFiles.AsReadOnly()
                };
                var extractionResults = await _extractionCoordinator
                    .ExtractAsync(extractionBatch, cancellationToken)
                    .ConfigureAwait(false);

                foreach (var extractionResult in extractionResults)
                {
                    messages.AddRange(extractionResult.Outcome.Messages);
                    if (extractionResult.Outcome.Value is not { } extractedDocument)
                    {
                        rows.Add(createFailedRow(extractionResult));
                        hadSkippedOrFailedSource = true;
                        continue;
                    }

                    if (extractedCharacters + extractedDocument.Content.Length
                        > _options.MaximumTotalExtractedCharacters)
                    {
                        var limitMessage = error(
                            "preparation.character-limit",
                            $"{extractionResult.SourceFile.RelativePath} would exceed the {_options.MaximumTotalExtractedCharacters:N0}-character batch limit.");
                        messages.Add(limitMessage);
                        rows.Add(createFailedRow(extractionResult.SourceFile, limitMessage.Message));
                        hadSkippedOrFailedSource = true;
                        continue;
                    }

                    extractedCharacters += extractedDocument.Content.Length;
                    var indexedDocument = extractedDocument with { CarrotDocumentIndex = documents.Count };
                    documents.Add(indexedDocument);
                    rows.Add(createReadyRow(indexedDocument));
                }
            }
            finally
            {
                try
                {
                    await loadedBatch.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    messages.Add(warning(
                        "preparation.cleanup",
                        $"Temporary ZIP cleanup could not be completed: {exception.Message}"));
                    hadSkippedOrFailedSource = true;
                }
            }
        }

        var preparedBatch = new PreparedDocumentBatch
        {
            Rows = rows.AsReadOnly(),
            Documents = documents.AsReadOnly()
        };

        if (documents.Count == 0)
        {
            messages.Add(error("preparation.no-ready-documents", "No documents were successfully prepared."));
            return OperationResult<PreparedDocumentBatch>.Failure(preparedBatch, messages);
        }

        return hadSkippedOrFailedSource
            ? OperationResult<PreparedDocumentBatch>.PartialSuccess(preparedBatch, messages)
            : OperationResult<PreparedDocumentBatch>.Success(preparedBatch, messages);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one ready review row and its bounded normalized terminal preview.</summary>
    private PreparedDocumentRow createReadyRow(ExtractedDocument document)
    {
        #region implementation

        var preview = createPreview(document.Content, _options.ConsolePreviewCharacterLimit);
        return new PreparedDocumentRow
        {
            SourceOrdinal = document.SourceFile.SourceOrdinal,
            CarrotDocumentIndex = document.CarrotDocumentIndex,
            ContainerPath = document.SourceFile.ContainerPath,
            RelativePath = document.SourceFile.RelativePath,
            FileName = document.SourceFile.FileName,
            Extension = document.SourceFile.Extension,
            SizeBytes = document.SourceFile.SizeBytes,
            Sha256 = document.Sha256,
            Status = PreparedDocumentStatus.Ready,
            ExtractedCharacterCount = document.Content.Length,
            ContentPreview = preview.Text,
            PreviewTruncated = preview.Truncated
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one failed review row from a structured extraction result.</summary>
    private static PreparedDocumentRow createFailedRow(ExtractionResult result)
    {
        #region implementation

        var errorMessage = string.Join(
            "; ",
            result.Outcome.Messages
                .Where(message => message.Severity == OperationMessageSeverity.Error)
                .Select(message => message.Message));
        return createFailedRow(result.SourceFile, errorMessage);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one failed review row from source metadata and a concise diagnostic.</summary>
    private static PreparedDocumentRow createFailedRow(SourceFile sourceFile, string errorMessage)
    {
        #region implementation

        return new PreparedDocumentRow
        {
            SourceOrdinal = sourceFile.SourceOrdinal,
            ContainerPath = sourceFile.ContainerPath,
            RelativePath = sourceFile.RelativePath,
            FileName = sourceFile.FileName,
            Extension = sourceFile.Extension,
            SizeBytes = sourceFile.SizeBytes,
            Status = PreparedDocumentStatus.Failed,
            ErrorMessage = errorMessage
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a whitespace-collapsed preview without first copying the complete normalized content.</summary>
    private static (string Text, bool Truncated) createPreview(string content, int limit)
    {
        #region implementation

        var preview = new StringBuilder(Math.Min(content.Length, limit));
        var pendingWhitespace = false;
        var truncated = false;

        foreach (var character in content)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingWhitespace = preview.Length > 0;
                continue;
            }

            if (pendingWhitespace && preview.Length < limit)
            {
                preview.Append(' ');
            }

            pendingWhitespace = false;
            if (preview.Length >= limit)
            {
                truncated = true;
                break;
            }

            preview.Append(character);
        }

        return (preview.ToString(), truncated);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one coded error message.</summary>
    private static OperationMessage error(string code, string message)
    {
        #region implementation

        return new OperationMessage { Code = code, Message = message, Severity = OperationMessageSeverity.Error };

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one coded warning message.</summary>
    private static OperationMessage warning(string code, string message)
    {
        #region implementation

        return new OperationMessage { Code = code, Message = message, Severity = OperationMessageSeverity.Warning };

        #endregion
    }

    #endregion
}
