namespace Carrot.Cli.Reporting;

/**************************************************************/
/// <summary>Defines shared atomic creation of generic single-sheet Excel workbooks.</summary>
/// <seealso cref="ExcelReportWriter"/>
internal interface IExcelWorkbookWriter
{
    /**************************************************************/
    /// <summary>Writes one typed worksheet table using the repository's atomic persistence rules.</summary>
    /// <param name="request">The destination, ordered columns, and aligned rows.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>A task representing complete workbook persistence.</returns>
    Task WriteAsync(ExcelWorkbookRequest request, CancellationToken cancellationToken);
}
