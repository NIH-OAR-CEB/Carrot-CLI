using Carrot.Cli.Input;

namespace Carrot.Cli.Extraction.Extractors;

/**************************************************************/
/// <summary>
/// Defines slide-order text and speaker-note extraction from PPTX presentations.
/// </summary>
/// <seealso cref="IDocumentTextExtractor"/>
internal sealed class PresentationExtractor : IDocumentTextExtractor
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the PPTX extension handled by this extractor.</summary>
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
    /// <summary>Extracts slide and speaker-note text from one PPTX source.</summary>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    public Task<ExtractionResult> ExtractAsync(SourceFile sourceFile, CancellationToken cancellationToken)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
