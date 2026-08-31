using System.Globalization;
using Carrot.Cli.Configuration;
using Carrot.Cli.Reporting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Carrot.Cli.Tests.Reporting;

/**************************************************************/
/// <summary>Verifies deterministic mapping from retained processed state to workbook rows.</summary>
public sealed class ProcessedDocumentReportMapperTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Verifies source, request, preview, and aligned membership fields use retained values.</summary>
    [Fact]
    public void Create_AssignedDocument_MapsCompleteDocumentAndInvariantMemberships()
    {
        #region implementation

        // Arrange
        var memberships = new ClusterMembership[]
        {
            new()
            {
                CarrotDocumentIndex = 0,
                Labels = ["Parent"],
                CategoryPath = "Parent > Child",
                Score = 0.12345678901234566D,
                Depth = 1
            },
            new()
            {
                CarrotDocumentIndex = 0,
                Labels = ["No score"],
                CategoryPath = "No score",
                Score = null,
                Depth = 0
            }
        };
        var batch = ReportingTestData.CreateProcessedBatch("=formula-looking content", memberships, "=formula.txt");
        var mapper = createMapper();
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

            // Act
            var request = mapper.Create(batch, "C:\\Reports\\results.xlsx", overwrite: true);

            // Assert
            Assert.True(request.Overwrite);
            Assert.Equal("C:\\Reports\\results.xlsx", request.OutputPath);
            var row = Assert.Single(request.Rows);
            Assert.Equal(ReportingTestData.RunId, row.RunId);
            Assert.Equal("Success", row.RunStatus);
            Assert.Equal("http://localhost:8080/service", row.Endpoint.TrimEnd('/'));
            Assert.Equal("Lingo", row.Algorithm);
            Assert.Equal("English", row.Language);
            Assert.Null(row.Template);
            Assert.Equal(7, row.SourceOrdinal);
            Assert.Equal(0, row.CarrotDocumentIndex);
            Assert.Equal("=formula.txt", row.FileName);
            Assert.Equal(new string('A', 64), row.Sha256);
            Assert.Equal("Ready", row.ExtractionStatus);
            Assert.Equal("=formula-looking content", row.ContentPreview);
            Assert.False(row.PreviewTruncated);
            Assert.Equal(2, row.CategoryCount);
            Assert.Equal($"Parent > Child{Environment.NewLine}No score", row.CategoryPaths);
            Assert.Equal($"0.12345678901234566{Environment.NewLine}", row.CategoryScores);
            Assert.Contains("\"categoryPath\":\"Parent > Child\"", row.CategoryMembershipsJson, StringComparison.Ordinal);
            Assert.Contains("\"score\":null", row.CategoryMembershipsJson, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies preview extraction at values around the configured 30,000-character boundary.</summary>
    /// <param name="contentLength">The complete retained content length.</param>
    /// <param name="expectedPreviewLength">The expected workbook preview length.</param>
    /// <param name="expectedTruncated">Whether the preview must report omitted content.</param>
    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(29_999, 29_999, false)]
    [InlineData(30_000, 30_000, false)]
    [InlineData(30_001, 30_000, true)]
    public void Create_ContentBoundary_TruncatesOnlyAboveConfiguredLimit(
        int contentLength,
        int expectedPreviewLength,
        bool expectedTruncated)
    {
        #region implementation

        // Arrange
        var batch = ReportingTestData.CreateProcessedBatch(new string('x', contentLength));
        var mapper = createMapper();

        // Act
        var row = Assert.Single(mapper.Create(batch, "C:\\Reports\\results.xlsx", overwrite: false).Rows);

        // Assert
        Assert.Equal(contentLength, row.ExtractedCharacterCount);
        Assert.Equal(expectedPreviewLength, row.ContentPreview!.Length);
        Assert.Equal(expectedTruncated, row.PreviewTruncated);
        Assert.Equal(0, row.CategoryCount);
        Assert.Null(row.CategoryPaths);
        Assert.Null(row.CategoryScores);
        Assert.Equal("[]", row.CategoryMembershipsJson);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates the mapper with the production 30,000-character workbook preview limit.</summary>
    /// <returns>The configured mapper.</returns>
    private static ProcessedDocumentReportMapper createMapper()
    {
        #region implementation

        return new ProcessedDocumentReportMapper(Options.Create(new CarrotCliOptions()));

        #endregion
    }

    #endregion
}
