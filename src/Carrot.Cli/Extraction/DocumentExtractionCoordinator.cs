using Carrot.Cli.Input;

namespace Carrot.Cli.Extraction;

/**************************************************************/
/// <summary>
/// Selects extraction strategies, bounds concurrency, and preserves deterministic source order.
/// </summary>
/// <seealso cref="IDocumentTextExtractor"/>
internal sealed class DocumentExtractionCoordinator
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Initializes the coordinator with all registered extraction strategies.
    /// </summary>
    /// <param name="extractors">The extension-specific extraction strategies.</param>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    internal DocumentExtractionCoordinator(IEnumerable<IDocumentTextExtractor> extractors)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Extracts every source while retaining failures at their original ordinals.
    /// </summary>
    /// <param name="batch">The discovered input batch.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>A task containing results in deterministic source order.</returns>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    internal Task<IReadOnlyList<ExtractionResult>> ExtractAsync(InputBatch batch, CancellationToken cancellationToken)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
