namespace Carrot.Cli.Reporting;

/**************************************************************/
/// <summary>
/// Defines ClosedXML-based generation of the single Results worksheet.
/// </summary>
/// <seealso cref="IExcelReportWriter"/>
internal sealed class ExcelReportWriter : IExcelReportWriter
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Writes the ordered Results worksheet as an atomic XLSX artifact.
    /// </summary>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    public Task WriteAsync(ReportRequest request, CancellationToken cancellationToken)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
