namespace Carrot.Cli.Processing;

/**************************************************************/
/// <summary>Models one compact console-review row produced during document preparation.</summary>
/// <remarks>
/// The row deliberately contains a short normalized preview rather than complete extracted text.
/// Complete text is retained only in the paired successful document collection.
/// </remarks>
/// <seealso cref="PreparedDocumentBatch"/>
internal sealed record PreparedDocumentRow
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the zero-based global source ordinal.</summary>
    public int SourceOrdinal { get; init; }

    /**************************************************************/
    /// <summary>Gets the zero-based future Carrot document index when preparation succeeded.</summary>
    public int? CarrotDocumentIndex { get; init; }

    /**************************************************************/
    /// <summary>Gets the original folder, direct-file parent, or ZIP container path.</summary>
    public required string ContainerPath { get; init; }

    /**************************************************************/
    /// <summary>Gets the normalized relative document path.</summary>
    public required string RelativePath { get; init; }

    /**************************************************************/
    /// <summary>Gets the source filename including extension.</summary>
    public required string FileName { get; init; }

    /**************************************************************/
    /// <summary>Gets the lowercase source extension including its leading period.</summary>
    public required string Extension { get; init; }

    /**************************************************************/
    /// <summary>Gets the source size in bytes.</summary>
    public long SizeBytes { get; init; }

    /**************************************************************/
    /// <summary>Gets the SHA-256 digest when preparation succeeded.</summary>
    public string? Sha256 { get; init; }

    /**************************************************************/
    /// <summary>Gets whether this row is ready or failed.</summary>
    public PreparedDocumentStatus Status { get; init; }

    /**************************************************************/
    /// <summary>Gets the complete extracted character count for ready documents.</summary>
    public int ExtractedCharacterCount { get; init; }

    /**************************************************************/
    /// <summary>Gets a whitespace-normalized console-safe-length content preview.</summary>
    public string? ContentPreview { get; init; }

    /**************************************************************/
    /// <summary>Gets whether more extracted content exists beyond the displayed preview.</summary>
    public bool PreviewTruncated { get; init; }

    /**************************************************************/
    /// <summary>Gets the concise expected failure description for failed rows.</summary>
    public string? ErrorMessage { get; init; }

    #endregion
}
