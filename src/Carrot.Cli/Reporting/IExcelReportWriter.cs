namespace Carrot.Cli.Reporting;

/**************************************************************/
/// <summary>
/// Defines atomic creation of the single-sheet Excel results workbook.
/// </summary>
/// <seealso cref="ExcelReportWriter"/>
internal interface IExcelReportWriter
{
    /**************************************************************/
    /// <summary>
    /// Writes the ordered Results worksheet while forcing all strings to safe text values.
    /// </summary>
    /// <param name="request">The complete workbook path, metadata, and row collection.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>A task representing atomic workbook persistence.</returns>
    Task WriteAsync(ReportRequest request, CancellationToken cancellationToken);
}
