using System.Text.Json;
using Carrot.Cli.ISearch;

namespace Carrot.Cli.Reporting;

/**************************************************************/
/// <summary>Maps categorized iSearch rows into the shared typed worksheet contract.</summary>
/// <remarks>
/// The mapper preserves result order and category multiplicity without introducing file metadata;
/// workbook construction and atomic persistence remain owned by the shared writer.
/// </remarks>
/// <seealso cref="CategorizedISearchResultBatch"/>
/// <seealso cref="ExcelWorkbookRequest"/>
internal sealed class CategorizedISearchResultsReportMapper
{
    #region implementation

    private static readonly ExcelWorksheetColumn[] Columns =
    [
        new() { Name = "ResultPage", Width = 16D },
        new() { Name = "ResultOrdinal", Width = 18D },
        new() { Name = "nihApplId", Width = 22D },
        new() { Name = "title", Width = 48D, WrapText = true },
        new() { Name = "abstract", Width = 70D, WrapText = true },
        new() { Name = "specificAims", Width = 70D, WrapText = true },
        new() { Name = "CategoryCount", Width = 18D },
        new() { Name = "CategoryPaths", Width = 48D, WrapText = true },
        new() { Name = "CategoryScores", Width = 36D, WrapText = true },
        new() { Name = "CategoryMembershipsJson", Width = 60D, WrapText = true }
    ];

    /**************************************************************/
    /// <summary>Creates a deterministic worksheet request for one categorized iSearch batch.</summary>
    /// <param name="batch">The categorized records in loaded service order.</param>
    /// <param name="outputPath">The normalized absolute workbook destination.</param>
    /// <param name="overwrite">Whether an existing workbook may be replaced.</param>
    /// <returns>A generic worksheet request consumed by the shared Excel writer.</returns>
    internal ExcelWorkbookRequest Create(
        CategorizedISearchResultBatch batch,
        string outputPath,
        bool overwrite)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(batch);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var rows = batch.Rows
            .OrderBy(row => row.ResultOrdinal)
            .SelectMany(createRows)
            .ToArray();
        return new ExcelWorkbookRequest
        {
            OutputPath = outputPath,
            Overwrite = overwrite,
            Columns = Columns,
            Rows = Array.AsReadOnly(rows)
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>Expands one iSearch record into membership rows or one unassigned row.</summary>
    /// <param name="row">The categorized iSearch record.</param>
    /// <returns>The ordered worksheet rows for the record.</returns>
    private static IReadOnlyList<IReadOnlyList<ExcelCellValue>> createRows(CategorizedISearchResultRow row)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(row);
        if (row.Memberships.Count == 0)
        {
            return [createRow(row, membership: null)];
        }

        return row.Memberships.Select(membership => createRow(row, membership)).ToArray();

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one typed worksheet row for one record/category pair.</summary>
    /// <param name="row">The source iSearch record.</param>
    /// <param name="membership">The represented category, or null for unassigned data.</param>
    /// <returns>Cells aligned to the categorized iSearch columns.</returns>
    private static IReadOnlyList<ExcelCellValue> createRow(
        CategorizedISearchResultRow row,
        ClusterMembership? membership)
    {
        #region implementation

        return
        [
            ExcelCellValue.Integer(row.ResultPage),
            ExcelCellValue.Integer(row.ResultOrdinal),
            createCell(row.NihApplId),
            createCell(row.Title),
            createCell(row.Abstract),
            createCell(row.SpecificAims),
            ExcelCellValue.Integer(membership is null ? 0 : 1),
            ExcelCellValue.Text(membership?.CategoryPath),
            ExcelCellValue.Text(membership?.Score?.ToString("R", System.Globalization.CultureInfo.InvariantCulture)),
            membership is null
                ? ExcelCellValue.Text("[]")
                : ExcelCellValue.Text(CategoryMembershipJsonFormatter.Serialize(membership))
        ];

        #endregion
    }

    /**************************************************************/
    /// <summary>Converts one retained JSON scalar into a safe typed Excel value.</summary>
    /// <param name="value">The detached iSearch field value.</param>
    /// <returns>An explicit Excel cell representation.</returns>
    private static ExcelCellValue createCell(JsonElement value)
    {
        #region implementation

        return value.ValueKind switch
        {
            JsonValueKind.String => ExcelCellValue.Text(value.GetString()),
            JsonValueKind.Number when value.TryGetInt64(out var integer) => ExcelCellValue.Long(integer),
            JsonValueKind.Number when value.TryGetDouble(out var number) && double.IsFinite(number)
                => ExcelCellValue.Double(number),
            JsonValueKind.Null => ExcelCellValue.Blank(),
            JsonValueKind.True => ExcelCellValue.Boolean(true),
            JsonValueKind.False => ExcelCellValue.Boolean(false),
            _ => ExcelCellValue.Text(value.GetRawText())
        };

        #endregion
    }

    #endregion
}
