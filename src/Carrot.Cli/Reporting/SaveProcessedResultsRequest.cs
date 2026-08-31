using Carrot.Cli.Processing;

namespace Carrot.Cli.Reporting;

/**************************************************************/
/// <summary>
/// Carries a retained processed batch, normalized workbook destination, and replacement policy.
/// </summary>
/// <seealso cref="IProcessedResultsExporter"/>
internal sealed record SaveProcessedResultsRequest
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the retained successful batch to export without reprocessing.</summary>
    public required ProcessedDocumentBatch Batch { get; init; }

    /**************************************************************/
    /// <summary>Gets the normalized absolute `.xlsx` destination.</summary>
    public required string OutputPath { get; init; }

    /**************************************************************/
    /// <summary>Gets whether an existing workbook may be replaced.</summary>
    public bool Overwrite { get; init; }

    #endregion
}
