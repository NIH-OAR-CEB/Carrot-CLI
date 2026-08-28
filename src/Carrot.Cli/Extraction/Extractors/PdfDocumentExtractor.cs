using Carrot.Cli.Input;
using System.Text;
using UglyToad.PdfPig;

namespace Carrot.Cli.Extraction.Extractors;

/**************************************************************/
/// <summary>
/// Defines searchable text-layer extraction from non-encrypted PDF documents without OCR.
/// </summary>
/// <seealso cref="IDocumentTextExtractor"/>
internal sealed class PdfDocumentExtractor : IDocumentTextExtractor
{
    #region implementation

    private static readonly IReadOnlySet<string> Extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pdf" };
    private readonly ExtractionResultFactory _resultFactory;

    /**************************************************************/
    /// <summary>Initializes PDF extraction with common result construction.</summary>
    public PdfDocumentExtractor(ExtractionResultFactory resultFactory)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(resultFactory);
        _resultFactory = resultFactory;

        #endregion
    }

    /**************************************************************/
    /// <summary>Gets the PDF extension handled by this extractor.</summary>
    public IReadOnlySet<string> SupportedExtensions
    {
        get
        {
            #region implementation

            return Extensions;

            #endregion
        }
    }

    /**************************************************************/
    /// <summary>Extracts the searchable text layer from one PDF source without OCR.</summary>
    public async Task<ExtractionResult> ExtractAsync(SourceFile sourceFile, CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(sourceFile);
        try
        {
            using var document = PdfDocument.Open(sourceFile.PhysicalPath);
            var content = new StringBuilder();
            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.IsNullOrWhiteSpace(page.Text))
                {
                    content.AppendLine(page.Text);
                }
            }

            return await _resultFactory
                .CreateSuccessAsync(sourceFile, content.ToString(), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return _resultFactory.CreateFailure(
                sourceFile,
                "extraction.pdf",
                $"Unable to read searchable PDF content: {exception.Message}");
        }

        #endregion
    }

    #endregion
}
