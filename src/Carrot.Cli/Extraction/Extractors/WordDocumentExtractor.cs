using Carrot.Cli.Input;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Carrot.Cli.Extraction.Extractors;

/**************************************************************/
/// <summary>
/// Defines current visible body and table text extraction from DOCX documents.
/// </summary>
/// <seealso cref="IDocumentTextExtractor"/>
internal sealed class WordDocumentExtractor : IDocumentTextExtractor
{
    #region implementation

    private static readonly IReadOnlySet<string> Extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".docx" };
    private readonly ExtractionResultFactory _resultFactory;

    /**************************************************************/
    /// <summary>Initializes Word extraction with common result construction.</summary>
    public WordDocumentExtractor(ExtractionResultFactory resultFactory)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(resultFactory);
        _resultFactory = resultFactory;

        #endregion
    }

    /**************************************************************/
    /// <summary>Gets the DOCX extension handled by this extractor.</summary>
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
    /// <summary>Extracts visible body and table text from one DOCX source.</summary>
    public async Task<ExtractionResult> ExtractAsync(SourceFile sourceFile, CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(sourceFile);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var document = WordprocessingDocument.Open(sourceFile.PhysicalPath, isEditable: false);
            var paragraphs = document.MainDocumentPart?.Document?.Body?
                .Descendants<Paragraph>()
                .Select(paragraph => string.Concat(paragraph.Descendants<Text>().Select(text => text.Text)))
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToArray()
                ?? Array.Empty<string>();

            return await _resultFactory
                .CreateSuccessAsync(sourceFile, string.Join(Environment.NewLine, paragraphs), cancellationToken)
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
                "extraction.docx",
                $"Unable to read Word content: {exception.Message}");
        }

        #endregion
    }

    #endregion
}
