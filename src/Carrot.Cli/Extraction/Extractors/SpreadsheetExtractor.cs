using Carrot.Cli.Input;

namespace Carrot.Cli.Extraction.Extractors;

/**************************************************************/
/// <summary>
/// Defines sheet-ordered nonempty-cell text extraction from XLSX workbooks.
/// </summary>
/// <seealso cref="IDocumentTextExtractor"/>
internal sealed class SpreadsheetExtractor : IDocumentTextExtractor
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the XLSX extension handled by this extractor.</summary>
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
    /// <summary>Extracts sheet-ordered nonempty cell text from one XLSX source.</summary>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    public Task<ExtractionResult> ExtractAsync(SourceFile sourceFile, CancellationToken cancellationToken)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
