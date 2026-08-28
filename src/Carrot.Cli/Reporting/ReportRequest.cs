namespace Carrot.Cli.Reporting;

/**************************************************************/
/// <summary>
/// Carries the final workbook destination, replacement policy, and ordered result rows.
/// </summary>
internal sealed record ReportRequest
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the final XLSX artifact path.</summary>
    public required string OutputPath { get; init; }

    /**************************************************************/
    /// <summary>Gets whether an existing workbook may be replaced.</summary>
    public bool Overwrite { get; init; }

    /**************************************************************/
    /// <summary>Gets rows in deterministic source order.</summary>
    public IReadOnlyList<ReportRow> Rows { get; init; } = Array.Empty<ReportRow>();

    #endregion
}
