namespace Carrot.Cli.Reporting;

/**************************************************************/
/// <summary>Carries one generic worksheet table to the shared atomic Excel writer.</summary>
/// <remarks>Rows must contain exactly one cell for each ordered column so dynamic iSearch data cannot shift columns silently.</remarks>
internal sealed record ExcelWorkbookRequest
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the final workbook path.</summary>
    public required string OutputPath { get; init; }

    /**************************************************************/
    /// <summary>Gets whether an existing workbook may be replaced.</summary>
    public bool Overwrite { get; init; }

    /**************************************************************/
    /// <summary>Gets the single worksheet name.</summary>
    public string WorksheetName { get; init; } = "Results";

    /**************************************************************/
    /// <summary>Gets the ordered worksheet columns.</summary>
    public IReadOnlyList<ExcelWorksheetColumn> Columns { get; init; } = Array.Empty<ExcelWorksheetColumn>();

    /**************************************************************/
    /// <summary>Gets the ordered rows whose cells align with <see cref="Columns"/>.</summary>
    public IReadOnlyList<IReadOnlyList<ExcelCellValue>> Rows { get; init; } = Array.Empty<IReadOnlyList<ExcelCellValue>>();

    #endregion
}
