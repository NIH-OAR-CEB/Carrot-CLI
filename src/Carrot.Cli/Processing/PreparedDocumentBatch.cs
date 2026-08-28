using Carrot.Cli.Extraction;

namespace Carrot.Cli.Processing;

/**************************************************************/
/// <summary>Contains review rows and the exact successfully extracted documents for one interactive batch.</summary>
/// <remarks>
/// ZIP temporary directories have already been removed when this value is returned. Future
/// processing must consume <see cref="Documents"/> rather than rereading physical source paths.
/// </remarks>
internal sealed record PreparedDocumentBatch
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets every unique discovered source row in deterministic order.</summary>
    public IReadOnlyList<PreparedDocumentRow> Rows { get; init; } = Array.Empty<PreparedDocumentRow>();

    /**************************************************************/
    /// <summary>Gets only successfully prepared documents with contiguous Carrot indexes.</summary>
    public IReadOnlyList<ExtractedDocument> Documents { get; init; } = Array.Empty<ExtractedDocument>();

    /**************************************************************/
    /// <summary>Gets the number of rows that failed preparation.</summary>
    public int FailedCount => Rows.Count(row => row.Status == PreparedDocumentStatus.Failed);

    #endregion
}
