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

    private static readonly IReadOnlySet<string> Extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".txt",
        ".md"
    };
    private readonly ExtractionResultFactory _resultFactory;

    /**************************************************************/
    /// <summary>Initializes plain-text extraction with common result construction.</summary>
    public PlainTextExtractor(ExtractionResultFactory resultFactory)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(resultFactory);
        _resultFactory = resultFactory;

        #endregion
    }

    /**************************************************************/
    /// <summary>Gets the TXT and Markdown extensions handled by this extractor.</summary>
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
    /// <summary>Extracts BOM-aware text from one TXT or Markdown source.</summary>
    public async Task<ExtractionResult> ExtractAsync(SourceFile sourceFile, CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(sourceFile);
        try
        {
            var content = await File.ReadAllTextAsync(sourceFile.PhysicalPath, cancellationToken).ConfigureAwait(false);
            return await _resultFactory.CreateSuccessAsync(sourceFile, content, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return _resultFactory.CreateFailure(
                sourceFile,
                "extraction.text",
                $"Unable to read text content: {exception.Message}");
        }

        #endregion
    }

    #endregion
}
