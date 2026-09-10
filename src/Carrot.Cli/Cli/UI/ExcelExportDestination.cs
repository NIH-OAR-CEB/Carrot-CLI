namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>Represents the approved destination and overwrite policy for an Excel export.</summary>
/// <remarks>The destination is produced only after the shared path validation and overwrite interaction completes.</remarks>
internal sealed record ExcelExportDestination
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the normalized absolute workbook destination.</summary>
    public required string OutputPath { get; init; }

    /**************************************************************/
    /// <summary>Gets whether an existing workbook may be replaced.</summary>
    public bool Overwrite { get; init; }

    #endregion
}
