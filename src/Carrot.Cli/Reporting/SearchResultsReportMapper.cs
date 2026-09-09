using System.Text.Json;
using Carrot.Cli.ISearch;
using Carrot.Cli.ISearch.Contracts;

namespace Carrot.Cli.Reporting;

/**************************************************************/
/// <summary>Maps all walked generic iSearch records into the shared Excel table contract.</summary>
/// <remarks>Configured fields lead the header order; additional returned properties are appended in first-seen order.</remarks>
/// <seealso cref="SearchResultPageSession"/>
/// <seealso cref="ExcelWorkbookRequest"/>
internal sealed class SearchResultsReportMapper
{
    #region implementation

    private const string ResultPageColumn = "ResultPage";
    private const string ResultOrdinalColumn = "ResultOrdinal";
    private const string ValueColumn = "Value";

    /**************************************************************/
    /// <summary>Creates one workbook request containing every page retained by the search session.</summary>
    /// <param name="session">The query session and its ordered walked pages.</param>
    /// <param name="outputPath">The normalized absolute workbook destination.</param>
    /// <param name="overwrite">Whether an existing workbook may be replaced.</param>
    /// <returns>An ordered generic worksheet request.</returns>
    internal ExcelWorkbookRequest Create(
        SearchResultPageSession session,
        string outputPath,
        bool overwrite)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var columnNames = createColumnNames(session);
        var columns = columnNames
            .Select(name => new ExcelWorksheetColumn
            {
                Name = name,
                Width = name is ResultPageColumn or ResultOrdinalColumn ? 16D : 36D,
                WrapText = name is not ResultPageColumn and not ResultOrdinalColumn
            })
            .ToArray();
        var rows = new List<IReadOnlyList<ExcelCellValue>>();
        var resultOrdinal = 0;

        // Enumerate pages first and records second so the workbook order mirrors the operator's
        // cursor walk exactly, including repeated records returned by the service.
        foreach (var page in session.WalkedPages)
        {
            for (var pageOrdinal = 0; pageOrdinal < page.Results.Count; pageOrdinal++)
            {
                rows.Add(createRow(
                    page,
                    pageOrdinal,
                    resultOrdinal++,
                    columnNames));
            }
        }

        return new ExcelWorkbookRequest
        {
            OutputPath = outputPath,
            Overwrite = overwrite,
            Columns = columns,
            Rows = rows.AsReadOnly()
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>Builds deterministic headers from configured fields and observed record properties.</summary>
    /// <param name="session">The session whose request and walked pages provide field names.</param>
    /// <returns>Unique ordered worksheet column names.</returns>
    private static IReadOnlyList<string> createColumnNames(SearchResultPageSession session)
    {
        #region implementation

        var names = new List<string> { ResultPageColumn, ResultOrdinalColumn };
        var seen = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        foreach (var field in session.Request.Fields)
        {
            if (!string.IsNullOrWhiteSpace(field) && seen.Add(field))
            {
                names.Add(field);
            }
        }

        var hasNonObjectRecord = false;
        foreach (var page in session.WalkedPages)
        {
            foreach (var record in page.Results)
            {
                if (record.ValueKind != JsonValueKind.Object)
                {
                    hasNonObjectRecord = true;
                    continue;
                }

                foreach (var property in record.EnumerateObject())
                {
                    if (seen.Add(property.Name))
                    {
                        names.Add(property.Name);
                    }
                }
            }
        }

        // A scalar or array root has no property names, so retain its complete JSON value in a
        // dedicated column rather than silently dropping a walked record.
        if (hasNonObjectRecord)
        {
            if (seen.Add(ValueColumn))
            {
                names.Add(ValueColumn);
            }
        }

        return names;

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one row with page provenance and every dynamic field value.</summary>
    /// <param name="page">The service page containing the record.</param>
    /// <param name="pageOrdinal">The zero-based ordinal within that service page.</param>
    /// <param name="resultOrdinal">The zero-based ordinal across the complete walked set.</param>
    /// <param name="columnNames">The previously resolved worksheet columns.</param>
    /// <returns>Cells aligned with the supplied columns.</returns>
    private static IReadOnlyList<ExcelCellValue> createRow(
        SearchResponse page,
        int pageOrdinal,
        int resultOrdinal,
        IReadOnlyList<string> columnNames)
    {
        #region implementation

        var record = page.Results[pageOrdinal];
        var cells = new List<ExcelCellValue>(columnNames.Count);
        foreach (var columnName in columnNames)
        {
            cells.Add(columnName switch
            {
                ResultPageColumn => ExcelCellValue.Integer(page.Cardinality.PageNumber),
                ResultOrdinalColumn => ExcelCellValue.Integer(resultOrdinal),
                ValueColumn when record.ValueKind != JsonValueKind.Object => createCell(record),
                ValueColumn => ExcelCellValue.Blank(),
                _ when record.ValueKind == JsonValueKind.Object
                    && record.TryGetProperty(columnName, out var property) => createCell(property),
                _ => ExcelCellValue.Blank()
            });
        }

        return cells;

        #endregion
    }

    /**************************************************************/
    /// <summary>Converts one JSON value to a safe native or textual Excel value.</summary>
    /// <param name="value">The generic iSearch JSON value.</param>
    /// <returns>An explicit typed-cell value.</returns>
    private static ExcelCellValue createCell(JsonElement value)
    {
        #region implementation

        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                return ExcelCellValue.Text(value.GetString());
            case JsonValueKind.Number:
                if (value.TryGetInt64(out var integer))
                {
                    return ExcelCellValue.Long(integer);
                }

                if (value.TryGetDouble(out var number) && double.IsFinite(number))
                {
                    return ExcelCellValue.Double(number);
                }

                return ExcelCellValue.Text(value.GetRawText());
            case JsonValueKind.True:
                return ExcelCellValue.Boolean(true);
            case JsonValueKind.False:
                return ExcelCellValue.Boolean(false);
            case JsonValueKind.Null:
                return ExcelCellValue.Blank();
            case JsonValueKind.Object:
            case JsonValueKind.Array:
                return ExcelCellValue.Text(JsonSerializer.Serialize(value));
            default:
                return ExcelCellValue.Text(value.GetRawText());
        }

        #endregion
    }

    #endregion
}
