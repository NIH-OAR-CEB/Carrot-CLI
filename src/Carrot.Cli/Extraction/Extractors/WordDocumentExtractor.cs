using Carrot.Cli.Input;

namespace Carrot.Cli.Extraction.Extractors;

/**************************************************************/
/// <summary>
/// Defines current visible body and table text extraction from DOCX documents.
/// </summary>
/// <seealso cref="IDocumentTextExtractor"/>
internal sealed class WordDocumentExtractor : IDocumentTextExtractor
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the DOCX extension handled by this extractor.</summary>
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
    /// <summary>Extracts visible body and table text from one DOCX source.</summary>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    public Task<ExtractionResult> ExtractAsync(SourceFile sourceFile, CancellationToken cancellationToken)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
