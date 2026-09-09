namespace Carrot.Cli.Reporting;

/**************************************************************/
/// <summary>Describes one ordered worksheet column and its bounded presentation hints.</summary>
internal sealed record ExcelWorksheetColumn
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the nonempty header text.</summary>
    public required string Name { get; init; }

    /**************************************************************/
    /// <summary>Gets the bounded display width applied to the column.</summary>
    public double Width { get; init; } = 18D;

    /**************************************************************/
    /// <summary>Gets whether cells in this column wrap long text.</summary>
    public bool WrapText { get; init; }

    #endregion
}
