using Xunit;

namespace Carrot.Cli.Tests.Reporting;

/**************************************************************/
/// <summary>
/// Reserves future acceptance coverage for workbook schema, safety, and atomic artifacts.
/// </summary>
public sealed class ExcelReportWriterTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Verifies the single Results sheet has all columns in exact documented order.</summary>
    [Fact(Skip = "Future acceptance: workbook generation is layout-only.")]
    public void WorkbookContainsTheExactResultsSchema()
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies 30,000-character previews, formula safety, memberships, and atomic replacement.</summary>
    [Fact(Skip = "Future acceptance: workbook safety and atomic output are layout-only.")]
    public void WorkbookAndSidecarsPreserveContentSafelyAndAtomically()
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
