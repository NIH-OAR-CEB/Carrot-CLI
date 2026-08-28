using Carrot.Cli.Input;

namespace Carrot.Cli.Extraction.Extractors;

/**************************************************************/
/// <summary>
/// Defines searchable text-layer extraction from non-encrypted PDF documents without OCR.
/// </summary>
/// <seealso cref="IDocumentTextExtractor"/>
internal sealed class PdfDocumentExtractor : IDocumentTextExtractor
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the PDF extension handled by this extractor.</summary>
    public IReadOnlySet<string> SupportedExtensions
    {
        get
        {
            #region implementation

            throw new NotImplementedException("Layout stub only.");

            #endregion
        }
    }

    /**************************************************************/
    /// <summary>Extracts the searchable text layer from one PDF source without OCR.</summary>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    public Task<ExtractionResult> ExtractAsync(SourceFile sourceFile, CancellationToken cancellationToken)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
