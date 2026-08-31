using Carrot.Cli.Common;
using ClosedXML.Excel;
using System.Globalization;

namespace Carrot.Cli.Reporting;

/**************************************************************/
/// <summary>
/// Defines ClosedXML-based generation of the single Results worksheet.
/// </summary>
/// <seealso cref="IExcelReportWriter"/>
internal sealed class ExcelReportWriter : IExcelReportWriter
{
    #region implementation

    private static readonly string[] Headers =
    [
        "RunId",
        "RunStatus",
        "Endpoint",
        "Algorithm",
        "Language",
        "Template",
        "SourceOrdinal",
        "CarrotDocumentIndex",
        "ContainerPath",
        "RelativePath",
        "FileName",
        "Extension",
        "SizeBytes",
        "SHA256",
        "ExtractionStatus",
        "ErrorMessage",
        "ExtractedCharacterCount",
        "ContentPreview",
        "PreviewTruncated",
        "CategoryCount",
        "CategoryPaths",
        "CategoryScores",
        "CategoryMembershipsJson"
    ];

    private readonly AtomicFileWriter _atomicFileWriter;

    /**************************************************************/
    /// <summary>Initializes workbook generation with the same-directory atomic persistence boundary.</summary>
    /// <param name="atomicFileWriter">The temporary-write and promotion coordinator.</param>
    public ExcelReportWriter(AtomicFileWriter atomicFileWriter)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(atomicFileWriter);
        _atomicFileWriter = atomicFileWriter;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Writes the ordered Results worksheet as an atomic XLSX artifact.
    /// </summary>
    /// <param name="request">The final path, overwrite policy, and deterministic report rows.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>A task representing complete atomic workbook persistence.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
    public Task WriteAsync(ReportRequest request, CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Rows);

        return _atomicFileWriter.WriteAsync(
            request.OutputPath,
            (stream, token) => writeWorkbookAsync(stream, request.Rows, token),
            request.Overwrite,
            cancellationToken);

        #endregion
    }

    /**************************************************************/
    /// <summary>Builds the complete single-sheet workbook into the supplied temporary stream.</summary>
    /// <param name="stream">The same-directory temporary stream owned by the atomic writer.</param>
    /// <param name="rows">The deterministic worksheet rows.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>A completed task after ClosedXML serializes the workbook.</returns>
    private static Task writeWorkbookAsync(
        Stream stream,
        IReadOnlyList<ReportRow> rows,
        CancellationToken cancellationToken)
    {
        #region implementation

        cancellationToken.ThrowIfCancellationRequested();
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Results");

        for (var columnIndex = 0; columnIndex < Headers.Length; columnIndex++)
        {
            setText(worksheet.Cell(1, columnIndex + 1), Headers[columnIndex]);
        }

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            writeRow(worksheet, rowIndex + 2, rows[rowIndex]);
        }

        formatWorksheet(worksheet, rows.Count + 1);
        cancellationToken.ThrowIfCancellationRequested();
        workbook.SaveAs(stream);
        return Task.CompletedTask;

        #endregion
    }

    /**************************************************************/
    /// <summary>Writes one report row with explicit text, numeric, and Boolean cell types.</summary>
    /// <param name="worksheet">The Results worksheet receiving the row.</param>
    /// <param name="rowNumber">The one-based worksheet row number.</param>
    /// <param name="row">The mapped report value.</param>
    private static void writeRow(IXLWorksheet worksheet, int rowNumber, ReportRow row)
    {
        #region implementation

        setText(worksheet.Cell(rowNumber, 1), row.RunId.ToString("D"));
        setText(worksheet.Cell(rowNumber, 2), row.RunStatus);
        setText(worksheet.Cell(rowNumber, 3), row.Endpoint);
        setText(worksheet.Cell(rowNumber, 4), row.Algorithm);
        setText(worksheet.Cell(rowNumber, 5), row.Language);
        setText(worksheet.Cell(rowNumber, 6), row.Template);
        worksheet.Cell(rowNumber, 7).SetValue(row.SourceOrdinal);
        if (row.CarrotDocumentIndex is { } carrotDocumentIndex)
        {
            worksheet.Cell(rowNumber, 8).SetValue(carrotDocumentIndex);
        }

        setText(worksheet.Cell(rowNumber, 9), row.ContainerPath);
        setText(worksheet.Cell(rowNumber, 10), row.RelativePath);
        setText(worksheet.Cell(rowNumber, 11), row.FileName);
        setText(worksheet.Cell(rowNumber, 12), row.Extension);
        worksheet.Cell(rowNumber, 13).SetValue(row.SizeBytes);
        setText(worksheet.Cell(rowNumber, 14), row.Sha256);
        setText(worksheet.Cell(rowNumber, 15), row.ExtractionStatus);
        setText(worksheet.Cell(rowNumber, 16), row.ErrorMessage);
        worksheet.Cell(rowNumber, 17).SetValue(row.ExtractedCharacterCount);
        setText(worksheet.Cell(rowNumber, 18), row.ContentPreview);
        worksheet.Cell(rowNumber, 19).SetValue(row.PreviewTruncated);
        worksheet.Cell(rowNumber, 20).SetValue(row.CategoryCount);
        setText(worksheet.Cell(rowNumber, 21), row.CategoryPaths);
        setText(worksheet.Cell(rowNumber, 22), row.CategoryScores);
        setText(worksheet.Cell(rowNumber, 23), row.CategoryMembershipsJson);

        #endregion
    }

    /**************************************************************/
    /// <summary>Writes an untrusted string as inert Excel text without altering its displayed value.</summary>
    /// <param name="cell">The destination worksheet cell.</param>
    /// <param name="value">The optional string value; null becomes an empty text cell.</param>
    private static void setText(IXLCell cell, string? value)
    {
        #region implementation

        cell.SetValue(XLCellValue.FromObject(value ?? string.Empty, CultureInfo.InvariantCulture));

        #endregion
    }

    /**************************************************************/
    /// <summary>Applies bounded readable formatting without sizing columns from large content previews.</summary>
    /// <param name="worksheet">The completed Results worksheet.</param>
    /// <param name="lastRowNumber">The last populated row, including the header-only case.</param>
    private static void formatWorksheet(IXLWorksheet worksheet, int lastRowNumber)
    {
        #region implementation

        var header = worksheet.Range(1, 1, 1, Headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#F4B183");
        header.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        worksheet.Range(1, 1, lastRowNumber, Headers.Length).SetAutoFilter();
        worksheet.SheetView.FreezeRows(1);
        if (lastRowNumber >= 2)
        {
            worksheet.Rows(2, lastRowNumber).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        }

        worksheet.Column(1).Width = 38;
        worksheet.Column(2).Width = 16;
        worksheet.Column(3).Width = 38;
        worksheet.Columns(4, 6).Width = 16;
        worksheet.Columns(7, 8).Width = 20;
        worksheet.Columns(9, 11).Width = 36;
        worksheet.Column(12).Width = 12;
        worksheet.Column(13).Width = 16;
        worksheet.Column(14).Width = 68;
        worksheet.Columns(15, 17).Width = 22;
        worksheet.Column(18).Width = 70;
        worksheet.Columns(19, 20).Width = 18;
        worksheet.Columns(21, 23).Width = 48;
        worksheet.Columns(18, 23).Style.Alignment.WrapText = true;

        #endregion
    }

    #endregion
}
