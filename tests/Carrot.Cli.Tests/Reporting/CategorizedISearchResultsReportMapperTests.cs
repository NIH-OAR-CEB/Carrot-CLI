using System.Text.Json;
using Carrot.Cli.CarrotApi.Contracts;
using Carrot.Cli.Configuration;
using Carrot.Cli.ISearch;
using Carrot.Cli.Reporting;
using Xunit;

namespace Carrot.Cli.Tests.Reporting;

/**************************************************************/
/// <summary>Verifies the categorized iSearch workbook column and membership-row contract.</summary>
public sealed class CategorizedISearchResultsReportMapperTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Ensures source fields, provenance, categories, and unassigned records are exported.</summary>
    [Fact]
    public void Create_PreservesFieldsAndExpandsMemberships()
    {
        #region implementation

        var assigned = new CategorizedISearchResultRow
        {
            ResultPage = 1,
            ResultOrdinal = 0,
            NihApplId = JsonSerializer.SerializeToElement("1"),
            Title = JsonSerializer.SerializeToElement("First"),
            Abstract = JsonSerializer.SerializeToElement("Abstract"),
            SpecificAims = JsonSerializer.SerializeToElement("Aims"),
            Memberships =
            [
                new ClusterMembership
                {
                    CarrotDocumentIndex = 0,
                    CategoryPath = "Health",
                    Labels = ["Health"],
                    Score = 1.25D
                }
            ],
        };
        var unassigned = new CategorizedISearchResultRow
        {
            ResultPage = 1,
            ResultOrdinal = 1,
            NihApplId = JsonSerializer.SerializeToElement("2"),
            Title = JsonSerializer.SerializeToElement("Second"),
            Abstract = JsonSerializer.SerializeToElement((string?)null),
            SpecificAims = JsonSerializer.SerializeToElement("Aims two")
        };
        var batch = new CategorizedISearchResultBatch
        {
            RunId = Guid.NewGuid(),
            Endpoint = new Uri("http://localhost:8080/service"),
            Request = new ClusterRequest { Documents = [] },
            Configuration = new ClusteringConfiguration { Algorithm = "Lingo", Language = "English" },
            Response = new ClusterResponse(),
            Rows = [assigned, unassigned]
        };

        // Act
        var request = new CategorizedISearchResultsReportMapper().Create(batch, "categorized.xlsx", overwrite: false);

        // Assert
        Assert.Equal(
            ["ResultPage", "ResultOrdinal", "nihApplId", "title", "abstract", "specificAims", "CategoryCount", "CategoryPaths", "CategoryScores", "CategoryMembershipsJson"],
            request.Columns.Select(column => column.Name));
        Assert.Equal(2, request.Rows.Count);
        Assert.Equal("First", request.Rows[0][3].Value);
        Assert.Equal("Health", request.Rows[0][7].Value);
        Assert.Equal("1.25", request.Rows[0][8].Value);
        Assert.Contains("Health", (string)request.Rows[0][9].Value!);
        Assert.Equal(0, request.Rows[1][6].Value);
        Assert.Equal(ExcelCellKind.Blank, request.Rows[1][4].Kind);
        Assert.Equal("[]", request.Rows[1][9].Value);

        #endregion
    }

    #endregion
}
