using System.Globalization;
using Carrot.Cli.Common;
using ClosedXML.Excel;

namespace Carrot.Cli.Reporting;

/**************************************************************/
/// <summary>Defines shared ClosedXML generation for processed and generic result workbooks.</summary>
/// <remarks>Both report types use one typed-cell writer and one atomic persistence boundary.</remarks>
/// <seealso cref="IExcelReportWriter"/>
/// <seealso cref="IExcelWorkbookWriter"/>
internal sealed class ExcelReportWriter : IExcelReportWriter, IExcelWorkbookWriter
{
    #region implementation

    private static readonly ExcelWorksheetColumn[] ProcessedResultColumns =
    [
        new() { Name = "RunId", Width = 38D },
        new() { Name = "RunStatus", Width = 16D },
        new() { Name = "Endpoint", Width = 38D },
        new() { Name = "Algorithm", Width = 16D },
        new() { Name = "Language", Width = 16D },
        new() { Name = "Template", Width = 16D },
        new() { Name = "SourceOrdinal", Width = 20D },
        new() { Name = "CarrotDocumentIndex", Width = 20D },
        new() { Name = "ContainerPath", Width = 36D },
        new() { Name = "RelativePath", Width = 36D },
        new() { Name = "FileName", Width = 36D },
        new() { Name = "Extension", Width = 12D },
        new() { Name = "SizeBytes", Width = 16D },
        new() { Name = "SHA256", Width = 68D },
        new() { Name = "ExtractionStatus", Width = 22D },
        new() { Name = "ErrorMessage", Width = 22D },
        new() { Name = "ExtractedCharacterCount", Width = 22D },
        new() { Name = "ContentPreview", Width = 70D, WrapText = true },
        new() { Name = "PreviewTruncated", Width = 18D },
        new() { Name = "CategoryCount", Width = 18D },
        new() { Name = "CategoryPaths", Width = 48D, WrapText = true },
        new() { Name = "CategoryScores", Width = 48D, WrapText = true },
        new() { Name = "CategoryMembershipsJson", Width = 48D, WrapText = true }
    ];

    private readonly AtomicFileWriter _atomicFileWriter;

    /**************************************************************/
    /// <summary>Initializes workbook generation with the shared atomic persistence boundary.</summary>
    /// <param name="atomicFileWriter">The temporary-write and promotion coordinator.</param>
    public ExcelReportWriter(AtomicFileWriter atomicFileWriter)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(atomicFileWriter);
        _atomicFileWriter = atomicFileWriter;

        #endregion
    }

    /**************************************************************/
    /// <summary>Converts the established processed-document report into the generic worksheet contract.</summary>
    /// <param name="request">The processed-document report destination and ordered rows.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>A task representing atomic workbook persistence.</returns>
    public Task WriteAsync(ReportRequest request, CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Rows);

        var workbookRequest = new ExcelWorkbookRequest
        {
            OutputPath = request.OutputPath,
            Overwrite = request.Overwrite,
            Columns = ProcessedResultColumns,
            Rows = request.Rows.Select(createProcessedRow).ToArray()
        };

        return WriteAsync(workbookRequest, cancellationToken);

        #endregion
    }

    /**************************************************************/
    /// <summary>Writes one generic typed worksheet as an atomic XLSX artifact.</summary>
    /// <param name="request">The final path, overwrite policy, columns, and aligned rows.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>A task representing complete workbook persistence.</returns>
    public Task WriteAsync(ExcelWorkbookRequest request, CancellationToken cancellationToken)
    {
        #region implementation

        validateRequest(request);

        return _atomicFileWriter.WriteAsync(
            request.OutputPath,
            (stream, token) => writeWorkbookAsync(stream, request, token),
            request.Overwrite,
            cancellationToken);

        #endregion
    }

    /**************************************************************/
    /// <summary>Maps one processed-document row into the generic typed-cell representation.</summary>
    /// <param name="row">The established processed-document workbook row.</param>
    /// <returns>Cells aligned with <see cref="ProcessedResultColumns"/>.</returns>
    private static IReadOnlyList<ExcelCellValue> createProcessedRow(ReportRow row)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(row);

        return
        [
            ExcelCellValue.Text(row.RunId.ToString("D")),
            ExcelCellValue.Text(row.RunStatus),
            ExcelCellValue.Text(row.Endpoint),
            ExcelCellValue.Text(row.Algorithm),
            ExcelCellValue.Text(row.Language),
            ExcelCellValue.Text(row.Template),
            ExcelCellValue.Integer(row.SourceOrdinal),
            row.CarrotDocumentIndex is { } index
                ? ExcelCellValue.Integer(index)
                : ExcelCellValue.Blank(),
            ExcelCellValue.Text(row.ContainerPath),
            ExcelCellValue.Text(row.RelativePath),
            ExcelCellValue.Text(row.FileName),
            ExcelCellValue.Text(row.Extension),
            ExcelCellValue.Long(row.SizeBytes),
            ExcelCellValue.Text(row.Sha256),
            ExcelCellValue.Text(row.ExtractionStatus),
            ExcelCellValue.Text(row.ErrorMessage),
            ExcelCellValue.Integer(row.ExtractedCharacterCount),
            ExcelCellValue.Text(row.ContentPreview),
            ExcelCellValue.Boolean(row.PreviewTruncated),
            ExcelCellValue.Integer(row.CategoryCount),
            ExcelCellValue.Text(row.CategoryPaths),
            ExcelCellValue.Text(row.CategoryScores),
            ExcelCellValue.Text(row.CategoryMembershipsJson)
        ];

        #endregion
    }

    /**************************************************************/
    /// <summary>Validates the generic table before any destination file is created.</summary>
    /// <param name="request">The workbook request under validation.</param>
    private static void validateRequest(ExcelWorkbookRequest request)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorksheetName);
        ArgumentNullException.ThrowIfNull(request.Columns);
        ArgumentNullException.ThrowIfNull(request.Rows);

        if (request.Columns.Count == 0)
        {
            throw new ArgumentException("An Excel workbook requires at least one column.", nameof(request));
        }

        var columnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in request.Columns)
        {
            ArgumentNullException.ThrowIfNull(column);
            ArgumentException.ThrowIfNullOrWhiteSpace(column.Name);
            if (!columnNames.Add(column.Name))
            {
                throw new ArgumentException("Excel column names must be unique.", nameof(request));
            }
            if (!double.IsFinite(column.Width) || column.Width <= 0D || column.Width > 255D)
            {
                throw new ArgumentOutOfRangeException(nameof(request), "Excel column widths must be finite and between 0 and 255.");
            }
        }

        // Reject misaligned rows before atomic writing so a programming error cannot produce a
        // workbook where later values appear under the wrong dynamic iSearch field.
        foreach (var row in request.Rows)
        {
            ArgumentNullException.ThrowIfNull(row);
            if (row.Count != request.Columns.Count)
            {
                throw new ArgumentException("Every Excel row must contain one cell per column.", nameof(request));
            }

            foreach (var cell in row)
            {
                ArgumentNullException.ThrowIfNull(cell);
            }
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Builds and serializes one worksheet into the atomic writer's temporary stream.</summary>
    /// <param name="stream">The same-directory temporary stream.</param>
    /// <param name="request">The validated generic workbook request.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>A completed task after ClosedXML serializes the workbook.</returns>
    private static Task writeWorkbookAsync(
        Stream stream,
        ExcelWorkbookRequest request,
        CancellationToken cancellationToken)
    {
        #region implementation

        cancellationToken.ThrowIfCancellationRequested();
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(request.WorksheetName);

        for (var columnIndex = 0; columnIndex < request.Columns.Count; columnIndex++)
        {
            setText(worksheet.Cell(1, columnIndex + 1), request.Columns[columnIndex].Name);
        }

        for (var rowIndex = 0; rowIndex < request.Rows.Count; rowIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = request.Rows[rowIndex];
            for (var columnIndex = 0; columnIndex < row.Count; columnIndex++)
            {
                writeCell(worksheet.Cell(rowIndex + 2, columnIndex + 1), row[columnIndex]);
            }
        }

        formatWorksheet(worksheet, request.Columns, request.Rows.Count + 1);
        cancellationToken.ThrowIfCancellationRequested();
        workbook.SaveAs(stream);
        return Task.CompletedTask;

        #endregion
    }

    /**************************************************************/
    /// <summary>Writes one typed cell while preserving text as inert Excel content.</summary>
    /// <param name="cell">The destination worksheet cell.</param>
    /// <param name="value">The explicit cell value.</param>
    private static void writeCell(IXLCell cell, ExcelCellValue value)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(value);

        switch (value.Kind)
        {
            case ExcelCellKind.Text:
                setText(cell, (string?)value.Value);
                break;
            case ExcelCellKind.Blank:
                cell.Clear();
                break;
            case ExcelCellKind.Integer:
                cell.SetValue((int)value.Value!);
                break;
            case ExcelCellKind.Long:
                cell.SetValue((long)value.Value!);
                break;
            case ExcelCellKind.Double:
                cell.SetValue((double)value.Value!);
                break;
            case ExcelCellKind.Boolean:
                cell.SetValue((bool)value.Value!);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(value), value.Kind, "Unsupported Excel cell kind.");
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Writes an untrusted string as inert Excel text without changing its display value.</summary>
    /// <param name="cell">The destination worksheet cell.</param>
    /// <param name="value">The optional string value.</param>
    private static void setText(IXLCell cell, string? value)
    {
        #region implementation

        cell.SetValue(XLCellValue.FromObject(value ?? string.Empty, CultureInfo.InvariantCulture));

        #endregion
    }

    /**************************************************************/
    /// <summary>Applies bounded, readable formatting to the generic worksheet.</summary>
    /// <param name="worksheet">The completed Results worksheet.</param>
    /// <param name="columns">The ordered column presentation hints.</param>
    /// <param name="lastRowNumber">The last populated row, including the header-only case.</param>
    private static void formatWorksheet(
        IXLWorksheet worksheet,
        IReadOnlyList<ExcelWorksheetColumn> columns,
        int lastRowNumber)
    {
        #region implementation

        var header = worksheet.Range(1, 1, 1, columns.Count);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#F4B183");
        header.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        worksheet.Range(1, 1, lastRowNumber, columns.Count).SetAutoFilter();
        worksheet.SheetView.FreezeRows(1);

        for (var index = 0; index < columns.Count; index++)
        {
            var column = columns[index];
            worksheet.Column(index + 1).Width = column.Width;
            if (column.WrapText)
            {
                worksheet.Column(index + 1).Style.Alignment.WrapText = true;
            }
        }

        if (lastRowNumber >= 2)
        {
            worksheet.Rows(2, lastRowNumber).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        }

        #endregion
    }

    #endregion
}
