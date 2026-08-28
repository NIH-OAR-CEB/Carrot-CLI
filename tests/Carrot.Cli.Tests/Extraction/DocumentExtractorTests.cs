using Xunit;

namespace Carrot.Cli.Tests.Extraction;

/**************************************************************/
/// <summary>
/// Reserves future acceptance coverage for every supported document extraction strategy.
/// </summary>
public sealed class DocumentExtractorTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Verifies DOCX, XLSX, PPTX, TXT, Markdown, and searchable PDF extraction rules.</summary>
    [Fact(Skip = "Future acceptance: all extractor implementations are layout-only.")]
    public void ExtractorsProduceOneSearchableDocumentPerSupportedFile()
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies corrupt, encrypted, image-only, and empty documents become structured failures.</summary>
    [Fact(Skip = "Future acceptance: file-level failure handling is layout-only.")]
    public void UnsupportedContentBecomesAFileLevelFailure()
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
