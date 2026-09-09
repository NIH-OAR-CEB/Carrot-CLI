using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Carrot.Cli.Reporting;
using ClosedXML.Excel;
using Microsoft.Extensions.Options;
using Xunit;

namespace Carrot.Cli.Tests.Reporting;

/**************************************************************/
/// <summary>Verifies the active workbook schema, cell safety, formatting, and atomic cancellation.</summary>
public sealed class ExcelReportWriterTests
{
    #region implementation

    private static readonly string[] ExpectedHeaders =
    [
        "RunId", "RunStatus", "Endpoint", "Algorithm", "Language", "Template",
        "SourceOrdinal", "CarrotDocumentIndex", "ContainerPath", "RelativePath",
        "FileName", "Extension", "SizeBytes", "SHA256", "ExtractionStatus", "ErrorMessage",
        "ExtractedCharacterCount", "ContentPreview", "PreviewTruncated",
        "CategoryCount", "CategoryPaths", "CategoryScores", "CategoryMembershipsJson"
    ];

    /**************************************************************/
    /// <summary>Verifies the public writer produces the exact typed Results schema with inert untrusted text.</summary>
    [Fact]
    public async Task WriteAsync_CompleteRow_WritesExactFormulaSafeResultsWorkbook()
    {
        #region implementation

        var root = createTemporaryDirectory();
        try
        {
            // Arrange
            var outputPath = Path.Combine(root, "results.xlsx");
            var preview = $"+SUM(A1:A2)\n{new string('x', 29_980)}";
            var request = new ReportRequest
            {
                OutputPath = outputPath,
                Rows =
                [
                    new ReportRow
                    {
                        RunId = ReportingTestData.RunId,
                        RunStatus = "Success",
                        Endpoint = "http://localhost:8080/service",
                        Algorithm = "Lingo",
                        Language = "English",
                        SourceOrdinal = 7,
                        CarrotDocumentIndex = 0,
                        ContainerPath = "C:\\=Inputs",
                        RelativePath = "=formula.txt",
                        FileName = "=formula.txt",
                        Extension = ".txt",
                        SizeBytes = 1234,
                        Sha256 = new string('A', 64),
                        ExtractionStatus = "Ready",
                        ExtractedCharacterCount = 30_001,
                        ContentPreview = preview,
                        PreviewTruncated = true,
                        CategoryCount = 1,
                        CategoryPaths = "Parent > Child",
                        CategoryScores = "0.25",
                        CategoryMembershipsJson = "[{\"categoryPath\":\"=unsafe\"}]"
                    }
                ]
            };
            var writer = new ExcelReportWriter(new AtomicFileWriter());

            // Act
            await writer.WriteAsync(request, TestContext.Current.CancellationToken);

            // Assert
            using var workbook = new XLWorkbook(outputPath);
            var worksheet = Assert.Single(workbook.Worksheets);
            Assert.Equal("Results", worksheet.Name);
            Assert.Equal(
                ExpectedHeaders,
                Enumerable.Range(1, ExpectedHeaders.Length).Select(column => worksheet.Cell(1, column).GetString()));
            Assert.Equal(2, worksheet.LastRowUsed()!.RowNumber());
            Assert.Equal(ReportingTestData.RunId.ToString("D"), worksheet.Cell(2, 1).GetString());
            Assert.Equal("=formula.txt", worksheet.Cell(2, 11).GetString());
            Assert.Equal(preview, worksheet.Cell(2, 18).GetString());
            Assert.Equal("[{\"categoryPath\":\"=unsafe\"}]", worksheet.Cell(2, 23).GetString());
            Assert.Equal(XLDataType.Text, worksheet.Cell(2, 11).DataType);
            Assert.Equal(XLDataType.Text, worksheet.Cell(2, 18).DataType);
            Assert.Equal(XLDataType.Text, worksheet.Cell(2, 23).DataType);
            Assert.False(worksheet.Cell(2, 11).HasFormula);
            Assert.False(worksheet.Cell(2, 18).HasFormula);
            Assert.False(worksheet.Cell(2, 23).HasFormula);
            Assert.Equal(XLDataType.Number, worksheet.Cell(2, 7).DataType);
            Assert.Equal(XLDataType.Number, worksheet.Cell(2, 13).DataType);
            Assert.Equal(XLDataType.Boolean, worksheet.Cell(2, 19).DataType);
            Assert.True(worksheet.Cell(1, 1).Style.Font.Bold);
            Assert.True(worksheet.Cell(2, 18).Style.Alignment.WrapText);
            Assert.True(worksheet.AutoFilter.IsEnabled);
            Assert.DoesNotContain(
                Directory.EnumerateFiles(root),
                path => Path.GetFileName(path).StartsWith(".results.xlsx.", StringComparison.Ordinal));
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies multiple memberships are exported as separate scalar-category worksheet rows.</summary>
    [Fact]
    public async Task WriteAsync_MultipleMemberships_WritesOneCategoryPerWorksheetRow()
    {
        #region implementation

        var root = createTemporaryDirectory();
        try
        {
            // Arrange
            var outputPath = Path.Combine(root, "membership-rows.xlsx");
            var memberships = new ClusterMembership[]
            {
                new()
                {
                    CarrotDocumentIndex = 0,
                    Labels = ["First"],
                    CategoryPath = "Parent > First",
                    Score = 10.25D,
                    Depth = 1
                },
                new()
                {
                    CarrotDocumentIndex = 0,
                    Labels = ["Second"],
                    CategoryPath = "Second",
                    Score = null,
                    Depth = 0
                }
            };
            var batch = ReportingTestData.CreateProcessedBatch(memberships: memberships);
            var mapper = new ProcessedDocumentReportMapper(Options.Create(new CarrotCliOptions()));
            var request = mapper.Create(batch, outputPath, overwrite: false);
            var writer = new ExcelReportWriter(new AtomicFileWriter());

            // Act
            await writer.WriteAsync(request, TestContext.Current.CancellationToken);

            // Assert
            using var workbook = new XLWorkbook(outputPath);
            var worksheet = workbook.Worksheet("Results");
            Assert.Equal(3, worksheet.LastRowUsed()!.RowNumber());
            Assert.Equal(0D, worksheet.Cell(2, 8).GetDouble());
            Assert.Equal(0D, worksheet.Cell(3, 8).GetDouble());
            Assert.Equal(1D, worksheet.Cell(2, 20).GetDouble());
            Assert.Equal(1D, worksheet.Cell(3, 20).GetDouble());
            Assert.Equal("Parent > First", worksheet.Cell(2, 21).GetString());
            Assert.Equal("Second", worksheet.Cell(3, 21).GetString());
            Assert.Equal("10.25", worksheet.Cell(2, 22).GetString());
            Assert.Equal(string.Empty, worksheet.Cell(3, 22).GetString());
            Assert.DoesNotContain(Environment.NewLine, worksheet.Cell(2, 21).GetString(), StringComparison.Ordinal);
            Assert.DoesNotContain(Environment.NewLine, worksheet.Cell(3, 21).GetString(), StringComparison.Ordinal);
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies cancellation before public writer execution creates no final or temporary workbook.</summary>
    [Fact]
    public async Task WriteAsync_CanceledToken_CreatesNoArtifact()
    {
        #region implementation

        var root = createTemporaryDirectory();
        try
        {
            // Arrange
            var outputPath = Path.Combine(root, "cancelled.xlsx");
            var writer = new ExcelReportWriter(new AtomicFileWriter());
            using var cancellationSource = new CancellationTokenSource();
            cancellationSource.Cancel();

            // Act
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writer.WriteAsync(
                new ReportRequest { OutputPath = outputPath },
                cancellationSource.Token));

            // Assert
            Assert.False(File.Exists(outputPath));
            Assert.Empty(Directory.EnumerateFiles(root));
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies the shared writer persists dynamic iSearch columns with safe native cell types.</summary>
    [Fact]
    public async Task WriteAsync_GenericTable_PreservesColumnsTypesAndFormulaSafety()
    {
        #region implementation

        var root = createTemporaryDirectory();
        try
        {
            var outputPath = Path.Combine(root, "isearch.xlsx");
            var request = new ExcelWorkbookRequest
            {
                OutputPath = outputPath,
                Columns =
                [
                    new() { Name = "ResultPage", Width = 16D },
                    new() { Name = "title", Width = 36D, WrapText = true },
                    new() { Name = "count", Width = 16D },
                    new() { Name = "active", Width = 16D },
                    new() { Name = "nested", Width = 36D, WrapText = true }
                ],
                Rows =
                [
                    new ExcelCellValue[]
                    {
                        ExcelCellValue.Integer(2),
                        ExcelCellValue.Text("=unsafe"),
                        ExcelCellValue.Long(42),
                        ExcelCellValue.Boolean(true),
                        ExcelCellValue.Text("{\"id\":1}")
                    }
                ]
            };

            await new ExcelReportWriter(new AtomicFileWriter()).WriteAsync(
                request,
                TestContext.Current.CancellationToken);

            using var workbook = new XLWorkbook(outputPath);
            var worksheet = Assert.Single(workbook.Worksheets);
            Assert.Equal("=unsafe", worksheet.Cell(2, 2).GetString());
            Assert.Equal(XLDataType.Text, worksheet.Cell(2, 2).DataType);
            Assert.False(worksheet.Cell(2, 2).HasFormula);
            Assert.Equal(XLDataType.Number, worksheet.Cell(2, 3).DataType);
            Assert.Equal(XLDataType.Boolean, worksheet.Cell(2, 4).DataType);
            Assert.True(worksheet.Cell(2, 5).Style.Alignment.WrapText);
            Assert.True(worksheet.AutoFilter.IsEnabled);
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one isolated workbook directory.</summary>
    /// <returns>The absolute directory path.</returns>
    private static string createTemporaryDirectory()
    {
        #region implementation

        var path = Path.Combine(Path.GetTempPath(), $"carrot-workbook-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;

        #endregion
    }

    /**************************************************************/
    /// <summary>Deletes one exact workbook test directory.</summary>
    /// <param name="path">The owned directory.</param>
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
