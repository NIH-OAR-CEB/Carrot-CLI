using System.Text;
using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Carrot.Cli.Extraction;
using Carrot.Cli.Extraction.Extractors;
using Carrot.Cli.Input;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.Extensions.Options;
using Xunit;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Carrot.Cli.Tests.Extraction;

/**************************************************************/
/// <summary>
/// Verifies every supported extraction strategy and expected file-level failures.
/// </summary>
public sealed class DocumentExtractorTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Verifies DOCX, XLSX, PPTX, TXT, Markdown, and searchable PDF extraction rules.</summary>
    [Fact]
    public async Task ExtractorsProduceOneSearchableDocumentPerSupportedFile()
    {
        #region implementation

        // Arrange
        var root = createTemporaryDirectory();
        try
        {
            var paths = new[]
            {
                await createTextAsync(root, "plain.txt", "Plain searchable text"),
                await createTextAsync(root, "notes.md", "Markdown searchable text"),
                createWordDocument(root, "document.docx", "Word searchable text"),
                createSpreadsheet(root, "workbook.xlsx", "Spreadsheet searchable text"),
                createPresentation(root, "slides.pptx", "Presentation searchable text"),
                createPdf(root, "searchable.pdf", "PDF searchable text")
            };
            var batch = new InputBatch
            {
                ContainerPath = root,
                Files = paths.Select((path, index) => createSourceFile(root, path, index)).ToArray()
            };
            var coordinator = createCoordinator();

            // Act
            var results = await coordinator.ExtractAsync(batch, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(6, results.Count);
            Assert.All(results, result => Assert.Equal(OperationStatus.Success, result.Outcome.Status));
            Assert.Contains("Plain searchable text", results[0].Outcome.Value!.Content, StringComparison.Ordinal);
            Assert.Contains("Markdown searchable text", results[1].Outcome.Value!.Content, StringComparison.Ordinal);
            Assert.Contains("Word searchable text", results[2].Outcome.Value!.Content, StringComparison.Ordinal);
            Assert.Contains("Spreadsheet searchable text", results[3].Outcome.Value!.Content, StringComparison.Ordinal);
            Assert.Contains("Presentation searchable text", results[4].Outcome.Value!.Content, StringComparison.Ordinal);
            Assert.Contains("PDF searchable text", results[5].Outcome.Value!.Content, StringComparison.Ordinal);
            Assert.All(results, result => Assert.Matches("^[0-9A-F]{64}$", result.Outcome.Value!.Sha256));
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies empty text and corrupt packaged documents become structured failures.</summary>
    [Fact]
    public async Task UnsupportedContentBecomesAFileLevelFailure()
    {
        #region implementation

        // Arrange
        var root = createTemporaryDirectory();
        try
        {
            var emptyText = await createTextAsync(root, "empty.txt", "   \r\n");
            var corruptWord = await createTextAsync(root, "corrupt.docx", "not an Open XML package");
            var batch = new InputBatch
            {
                ContainerPath = root,
                Files = new[]
                {
                    createSourceFile(root, emptyText, 0),
                    createSourceFile(root, corruptWord, 1)
                }
            };

            // Act
            var results = await createCoordinator().ExtractAsync(batch, TestContext.Current.CancellationToken);

            // Assert
            Assert.All(results, result => Assert.Equal(OperationStatus.Failure, result.Outcome.Status));
            Assert.Contains(results[0].Outcome.Messages, message => message.Code == "extraction.empty");
            Assert.Contains(results[1].Outcome.Messages, message => message.Code == "extraction.docx");
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates the production extractor collection with deterministic default options.</summary>
    private static DocumentExtractionCoordinator createCoordinator()
    {
        #region implementation

        var resultFactory = new ExtractionResultFactory(new HashService());
        IDocumentTextExtractor[] extractors =
        [
            new PlainTextExtractor(resultFactory),
            new WordDocumentExtractor(resultFactory),
            new SpreadsheetExtractor(resultFactory),
            new PresentationExtractor(resultFactory),
            new PdfDocumentExtractor(resultFactory)
        ];
        return new DocumentExtractionCoordinator(extractors, Options.Create(new CarrotCliOptions()));

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one source descriptor for a generated test document.</summary>
    private static SourceFile createSourceFile(string root, string path, int ordinal)
    {
        #region implementation

        var information = new FileInfo(path);
        return new SourceFile
        {
            SourceOrdinal = ordinal,
            SourceKey = information.FullName,
            ContainerPath = root,
            PhysicalPath = information.FullName,
            RelativePath = information.Name,
            FileName = information.Name,
            Extension = information.Extension.ToLowerInvariant(),
            SizeBytes = information.Length
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>Writes one UTF-8 text or intentionally corrupt packaged-document test file.</summary>
    private static async Task<string> createTextAsync(string root, string fileName, string content)
    {
        #region implementation

        var path = Path.Combine(root, fileName);
        await File.WriteAllTextAsync(path, content, TestContext.Current.CancellationToken);
        return path;

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a minimal Word document containing one paragraph.</summary>
    private static string createWordDocument(string root, string fileName, string text)
    {
        #region implementation

        var path = Path.Combine(root, fileName);
        using var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new W.Document(new W.Body(new W.Paragraph(new W.Run(new W.Text(text)))));
        mainPart.Document.Save();
        return path;

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a minimal spreadsheet with one nonempty cell.</summary>
    private static string createSpreadsheet(string root, string fileName, string text)
    {
        #region implementation

        var path = Path.Combine(root, fileName);
        using var workbook = new XLWorkbook();
        workbook.AddWorksheet("Sheet One").Cell("A1").Value = text;
        workbook.SaveAs(path);
        return path;

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a minimal presentation containing one text shape.</summary>
    private static string createPresentation(string root, string fileName, string text)
    {
        #region implementation

        var path = Path.Combine(root, fileName);
        using var document = PresentationDocument.Create(path, PresentationDocumentType.Presentation);
        var presentationPart = document.AddPresentationPart();
        presentationPart.Presentation = new P.Presentation();
        var slidePart = presentationPart.AddNewPart<SlidePart>();
        slidePart.Slide = new P.Slide(
            new P.CommonSlideData(
                new P.ShapeTree(
                    new P.NonVisualGroupShapeProperties(
                        new P.NonVisualDrawingProperties { Id = 1U, Name = string.Empty },
                        new P.NonVisualGroupShapeDrawingProperties(),
                        new P.ApplicationNonVisualDrawingProperties()),
                    new P.GroupShapeProperties(new A.TransformGroup()),
                    new P.Shape(
                        new P.NonVisualShapeProperties(
                            new P.NonVisualDrawingProperties { Id = 2U, Name = "Text" },
                            new P.NonVisualShapeDrawingProperties(),
                            new P.ApplicationNonVisualDrawingProperties()),
                        new P.ShapeProperties(),
                        new P.TextBody(
                            new A.BodyProperties(),
                            new A.ListStyle(),
                            new A.Paragraph(new A.Run(new A.Text(text))))))));
        slidePart.Slide.Save();
        var slideIdList = new P.SlideIdList();
        slideIdList.Append(new P.SlideId
        {
            Id = 256U,
            RelationshipId = presentationPart.GetIdOfPart(slidePart)
        });
        presentationPart.Presentation.Append(slideIdList);
        presentationPart.Presentation.Save();
        return path;

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a minimal one-page PDF with a searchable Helvetica text stream.</summary>
    private static string createPdf(string root, string fileName, string text)
    {
        #region implementation

        var path = Path.Combine(root, fileName);
        var escapedText = text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
        var stream = $"BT /F1 12 Tf 72 720 Td ({escapedText}) Tj ET";
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}\nendstream"
        ];
        var builder = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
            builder.Append(index + 1).Append(" 0 obj\n").Append(objects[index]).Append("\nendobj\n");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(builder.ToString());
        builder.Append("xref\n0 6\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
        {
            builder.Append(offset.ToString("D10")).Append(" 00000 n \n");
        }

        builder.Append("trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n")
            .Append(xrefOffset)
            .Append("\n%%EOF\n");
        File.WriteAllBytes(path, Encoding.ASCII.GetBytes(builder.ToString()));
        return path;

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a uniquely named temporary test directory.</summary>
    private static string createTemporaryDirectory()
    {
        #region implementation

        var path = Path.Combine(Path.GetTempPath(), $"carrot-cli-extraction-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;

        #endregion
    }

    /**************************************************************/
    /// <summary>Deletes an owned test directory after resolving its exact absolute path.</summary>
    private static void deleteTemporaryDirectory(string path)
    {
        #region implementation

        var fullPath = Path.GetFullPath(path);
        if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, recursive: true);
        }

        #endregion
    }

    #endregion
}
