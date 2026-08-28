using Carrot.Cli.Input;

namespace Carrot.Cli.Extraction;

/**************************************************************/
/// <summary>
/// Defines one extension-specific searchable-text extraction strategy.
/// </summary>
internal interface IDocumentTextExtractor
{
    /**************************************************************/
    /// <summary>Gets the case-insensitive extensions supported by this strategy.</summary>
    IReadOnlySet<string> SupportedExtensions { get; }

    /**************************************************************/
    /// <summary>
    /// Extracts searchable text from one source file or returns an expected file-level failure.
    /// </summary>
    /// <param name="sourceFile">The source descriptor and physical extraction path.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>A task containing extracted text or structured failure messages.</returns>
    Task<ExtractionResult> ExtractAsync(SourceFile sourceFile, CancellationToken cancellationToken);
}
