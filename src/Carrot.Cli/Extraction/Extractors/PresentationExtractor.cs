using Carrot.Cli.Input;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using A = DocumentFormat.OpenXml.Drawing;

namespace Carrot.Cli.Extraction.Extractors;

/**************************************************************/
/// <summary>
/// Defines slide-order text and speaker-note extraction from PPTX presentations.
/// </summary>
/// <seealso cref="IDocumentTextExtractor"/>
internal sealed class PresentationExtractor : IDocumentTextExtractor
{
    #region implementation

    private static readonly IReadOnlySet<string> Extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pptx" };
    private readonly ExtractionResultFactory _resultFactory;

    /**************************************************************/
    /// <summary>Initializes presentation extraction with common result construction.</summary>
    public PresentationExtractor(ExtractionResultFactory resultFactory)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(resultFactory);
        _resultFactory = resultFactory;

        #endregion
    }

    /**************************************************************/
    /// <summary>Gets the PPTX extension handled by this extractor.</summary>
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
    /// <summary>Extracts slide and speaker-note text from one PPTX source.</summary>
    public async Task<ExtractionResult> ExtractAsync(SourceFile sourceFile, CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(sourceFile);
        try
        {
            using var document = PresentationDocument.Open(sourceFile.PhysicalPath, isEditable: false);
            var presentationPart = document.PresentationPart;
            var slideIds = presentationPart?.Presentation?.SlideIdList?.Elements<SlideId>()
                ?? Enumerable.Empty<SlideId>();
            var content = new StringBuilder();
            var slideNumber = 0;

            foreach (var slideId in slideIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                slideNumber++;
                var relationshipId = slideId.RelationshipId?.Value;
                if (string.IsNullOrWhiteSpace(relationshipId)
                    || presentationPart!.GetPartById(relationshipId) is not SlidePart slidePart)
                {
                    continue;
                }

                content.AppendLine($"Slide {slideNumber}");
                if (slidePart.Slide is not null)
                {
                    appendText(content, slidePart.Slide.Descendants<A.Text>());
                }

                var notesSlide = slidePart.NotesSlidePart?.NotesSlide;
                if (notesSlide is not null)
                {
                    appendText(content, notesSlide.Descendants<A.Text>());
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
                "extraction.pptx",
                $"Unable to read presentation content: {exception.Message}");
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Appends nonempty drawing-text values in their document order.</summary>
    private static void appendText(StringBuilder destination, IEnumerable<A.Text> values)
    {
        #region implementation

        foreach (var value in values.Select(text => text.Text).Where(text => !string.IsNullOrWhiteSpace(text)))
        {
            destination.AppendLine(value);
        }

        #endregion
    }

    #endregion
}
