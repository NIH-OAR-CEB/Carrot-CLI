namespace Carrot.Cli.Input;

/**************************************************************/
/// <summary>
/// Describes one supported source file while preserving container-relative identity and order.
/// </summary>
internal sealed record SourceFile
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the zero-based deterministic source ordinal.</summary>
    public int SourceOrdinal { get; init; }

    /**************************************************************/
    /// <summary>Gets the canonical case-insensitive identity used to remove duplicate discoveries.</summary>
    public required string SourceKey { get; init; }

    /**************************************************************/
    /// <summary>Gets the original folder or ZIP container path.</summary>
    public required string ContainerPath { get; init; }

    /**************************************************************/
    /// <summary>Gets the physical file path used by an extractor.</summary>
    public required string PhysicalPath { get; init; }

    /**************************************************************/
    /// <summary>Gets the normalized relative folder or archive-entry path.</summary>
    public required string RelativePath { get; init; }

    /**************************************************************/
    /// <summary>Gets the source file name including its extension.</summary>
    public required string FileName { get; init; }

    /**************************************************************/
    /// <summary>Gets the lowercase source extension including its leading period.</summary>
    public required string Extension { get; init; }

    /**************************************************************/
    /// <summary>Gets the source file size in bytes.</summary>
    public long SizeBytes { get; init; }

    #endregion
}
