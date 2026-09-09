namespace Carrot.Cli.Reporting;

/**************************************************************/
/// <summary>Represents one explicitly typed value written to an Excel worksheet cell.</summary>
/// <remarks>Explicit kinds let the shared writer preserve useful native values while treating untrusted strings as inert text.</remarks>
internal sealed record ExcelCellValue
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the cell kind used by the shared workbook writer.</summary>
    public ExcelCellKind Kind { get; init; }

    /**************************************************************/
    /// <summary>Gets the value associated with <see cref="Kind"/>, or <see langword="null"/> for a blank cell.</summary>
    public object? Value { get; init; }

    /**************************************************************/
    /// <summary>Creates an inert text cell, converting a null value to an empty string.</summary>
    /// <param name="value">The optional text value.</param>
    /// <returns>An explicitly text-typed cell value.</returns>
    internal static ExcelCellValue Text(string? value) => new()
    {
        Kind = ExcelCellKind.Text,
        Value = value ?? string.Empty
    };

    /**************************************************************/
    /// <summary>Creates a blank worksheet cell.</summary>
    /// <returns>A blank cell value.</returns>
    internal static ExcelCellValue Blank() => new() { Kind = ExcelCellKind.Blank };

    /**************************************************************/
    /// <summary>Creates an integer worksheet cell.</summary>
    /// <param name="value">The integer value.</param>
    /// <returns>A native integer cell value.</returns>
    internal static ExcelCellValue Integer(int value) => new()
    {
        Kind = ExcelCellKind.Integer,
        Value = value
    };

    /**************************************************************/
    /// <summary>Creates a long-integer worksheet cell.</summary>
    /// <param name="value">The long-integer value.</param>
    /// <returns>A native long-integer cell value.</returns>
    internal static ExcelCellValue Long(long value) => new()
    {
        Kind = ExcelCellKind.Long,
        Value = value
    };

    /**************************************************************/
    /// <summary>Creates a finite floating-point worksheet cell.</summary>
    /// <param name="value">The finite floating-point value.</param>
    /// <returns>A native floating-point cell value.</returns>
    internal static ExcelCellValue Double(double value) => new()
    {
        Kind = ExcelCellKind.Double,
        Value = value
    };

    /**************************************************************/
    /// <summary>Creates a native Boolean worksheet cell.</summary>
    /// <param name="value">The Boolean value.</param>
    /// <returns>A native Boolean cell value.</returns>
    internal static ExcelCellValue Boolean(bool value) => new()
    {
        Kind = ExcelCellKind.Boolean,
        Value = value
    };

    #endregion
}

/**************************************************************/
/// <summary>Identifies the native representation used for one Excel cell.</summary>
internal enum ExcelCellKind
{
    /**************************************************************/
    /// <summary>Stores the value as inert worksheet text.</summary>
    Text,

    /**************************************************************/
    /// <summary>Stores the value as a blank cell.</summary>
    Blank,

    /**************************************************************/
    /// <summary>Stores the value as a 32-bit integer.</summary>
    Integer,

    /**************************************************************/
    /// <summary>Stores the value as a 64-bit integer.</summary>
    Long,

    /**************************************************************/
    /// <summary>Stores the value as a double-precision number.</summary>
    Double,

    /**************************************************************/
    /// <summary>Stores the value as a native Boolean.</summary>
    Boolean
}
