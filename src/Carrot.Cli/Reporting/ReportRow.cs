namespace Carrot.Cli.Reporting;

/**************************************************************/
/// <summary>
/// Models one source-file row in the ordered Excel Results worksheet.
/// </summary>
/// <remarks>
/// Failed extraction rows retain source metadata and an empty Carrot document index.
/// Excel-bound strings are formatted as text to prevent formula evaluation.
/// </remarks>
internal sealed record ReportRow
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the run correlation identifier.</summary>
    public Guid RunId { get; init; }

    /**************************************************************/
    /// <summary>Gets the terminal run status text.</summary>
    public required string RunStatus { get; init; }

    /**************************************************************/
    /// <summary>Gets the absolute Carrot service endpoint text.</summary>
    public required string Endpoint { get; init; }

    /**************************************************************/
    /// <summary>Gets the selected algorithm identifier.</summary>
    public string? Algorithm { get; init; }

    /**************************************************************/
    /// <summary>Gets the selected language identifier.</summary>
    public string? Language { get; init; }

    /**************************************************************/
    /// <summary>Gets the optional selected template identifier.</summary>
    public string? Template { get; init; }

    /**************************************************************/
    /// <summary>Gets the zero-based deterministic source ordinal.</summary>
    public int SourceOrdinal { get; init; }

    /**************************************************************/
    /// <summary>Gets the zero-based submitted index, or null when extraction failed.</summary>
    public int? CarrotDocumentIndex { get; init; }

    /**************************************************************/
    /// <summary>Gets the original folder or ZIP container path.</summary>
    public required string ContainerPath { get; init; }

    /**************************************************************/
    /// <summary>Gets the normalized relative source path.</summary>
    public required string RelativePath { get; init; }

    /**************************************************************/
    /// <summary>Gets the source file name.</summary>
    public required string FileName { get; init; }

    /**************************************************************/
    /// <summary>Gets the normalized lowercase source extension.</summary>
    public required string Extension { get; init; }

    /**************************************************************/
    /// <summary>Gets the source size in bytes.</summary>
    public long SizeBytes { get; init; }

    /**************************************************************/
    /// <summary>Gets the uppercase hexadecimal SHA-256 digest when hashing succeeded.</summary>
    public string? Sha256 { get; init; }

    /**************************************************************/
    /// <summary>Gets the extraction success or failure status text.</summary>
    public required string ExtractionStatus { get; init; }

    /**************************************************************/
    /// <summary>Gets the file-level failure description when extraction was unsuccessful.</summary>
    public string? ErrorMessage { get; init; }

    /**************************************************************/
    /// <summary>Gets the complete extracted character count.</summary>
    public int ExtractedCharacterCount { get; init; }

    /**************************************************************/
    /// <summary>Gets at most 30,000 characters of extracted content for workbook inspection.</summary>
    public string? ContentPreview { get; init; }

    /**************************************************************/
    /// <summary>Gets whether the workbook content preview omits remaining extracted text.</summary>
    public bool PreviewTruncated { get; init; }

    /**************************************************************/
    /// <summary>Gets the number of exact cluster memberships assigned to this document.</summary>
    public int CategoryCount { get; init; }

    /**************************************************************/
    /// <summary>Gets full membership paths separated by Excel line breaks.</summary>
    public string? CategoryPaths { get; init; }

    /**************************************************************/
    /// <summary>Gets membership scores aligned with category paths.</summary>
    public string? CategoryScores { get; init; }

    /**************************************************************/
    /// <summary>Gets exact membership structures serialized as JSON.</summary>
    public string? CategoryMembershipsJson { get; init; }

    #endregion
}
