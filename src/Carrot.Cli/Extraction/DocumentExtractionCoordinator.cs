using Carrot.Cli.Input;
using Carrot.Cli.Configuration;
using Microsoft.Extensions.Options;

namespace Carrot.Cli.Extraction;

/**************************************************************/
/// <summary>
/// Selects extraction strategies, bounds concurrency, and preserves deterministic source order.
/// </summary>
/// <seealso cref="IDocumentTextExtractor"/>
internal sealed class DocumentExtractionCoordinator
{
    #region implementation

    private readonly IReadOnlyDictionary<string, IDocumentTextExtractor> _extractors;
    private readonly int _workerCount;

    /**************************************************************/
    /// <summary>
    /// Initializes the coordinator with all registered extraction strategies.
    /// </summary>
    /// <param name="extractors">The extension-specific extraction strategies.</param>
    /// <param name="options">The validated concurrency safeguards.</param>
    public DocumentExtractionCoordinator(
        IEnumerable<IDocumentTextExtractor> extractors,
        IOptions<CarrotCliOptions> options)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(extractors);
        ArgumentNullException.ThrowIfNull(options);

        var map = new Dictionary<string, IDocumentTextExtractor>(StringComparer.OrdinalIgnoreCase);
        foreach (var extractor in extractors)
        {
            foreach (var extension in extractor.SupportedExtensions)
            {
                if (!map.TryAdd(extension, extractor))
                {
                    throw new InvalidOperationException($"Multiple extractors are registered for {extension}.");
                }
            }
        }

        _extractors = map;
        _workerCount = options.Value.ExtractionWorkerCount;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Extracts every source while retaining failures at their original ordinals.
    /// </summary>
    /// <param name="batch">The discovered input batch.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>A task containing results in deterministic source order.</returns>
    internal async Task<IReadOnlyList<ExtractionResult>> ExtractAsync(
        InputBatch batch,
        CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(batch);
        using var gate = new SemaphoreSlim(_workerCount, _workerCount);
        var tasks = batch.Files
            .Select(sourceFile => extractOneAsync(sourceFile, gate, cancellationToken))
            .ToArray();

        // Task.WhenAll preserves the task-array order even when bounded workers complete out of order.
        return Array.AsReadOnly(await Task.WhenAll(tasks).ConfigureAwait(false));

        #endregion
    }

    /**************************************************************/
    /// <summary>Extracts one source after entering the shared bounded-concurrency gate.</summary>
    private async Task<ExtractionResult> extractOneAsync(
        SourceFile sourceFile,
        SemaphoreSlim gate,
        CancellationToken cancellationToken)
    {
        #region implementation

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_extractors.TryGetValue(sourceFile.Extension, out var extractor))
            {
                return new ExtractionResult
                {
                    SourceFile = sourceFile,
                    Outcome = Carrot.Cli.Common.OperationResult<ExtractedDocument>.Failure(
                    [
                        new Carrot.Cli.Common.OperationMessage
                        {
                            Code = "extraction.unsupported",
                            Message = $"No extractor is registered for {sourceFile.Extension}.",
                            Severity = Carrot.Cli.Common.OperationMessageSeverity.Error
                        }
                    ])
                };
            }

            return await extractor.ExtractAsync(sourceFile, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }

        #endregion
    }

    #endregion
}
