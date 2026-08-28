using Carrot.Cli.Input;

namespace Carrot.Cli.Extraction.Extractors;

/**************************************************************/
/// <summary>
/// Defines BOM-aware searchable-text extraction for TXT and Markdown documents.
/// </summary>
/// <seealso cref="IDocumentTextExtractor"/>
internal sealed class PlainTextExtractor : IDocumentTextExtractor
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the TXT and Markdown extensions handled by this extractor.</summary>
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
    /// <summary>Extracts BOM-aware text from one TXT or Markdown source.</summary>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    public Task<ExtractionResult> ExtractAsync(SourceFile sourceFile, CancellationToken cancellationToken)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
