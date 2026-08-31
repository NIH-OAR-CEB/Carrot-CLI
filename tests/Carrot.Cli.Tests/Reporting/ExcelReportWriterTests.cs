using Carrot.Cli.Common;
using Carrot.Cli.Reporting;
using ClosedXML.Excel;
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
                        CategoryCount = 2,
                        CategoryPaths = $"Parent > Child{Environment.NewLine}@Other",
                        CategoryScores = $"0.25{Environment.NewLine}",
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
