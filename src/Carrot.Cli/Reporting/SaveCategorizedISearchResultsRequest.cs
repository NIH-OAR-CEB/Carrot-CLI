using Carrot.Cli.ISearch;

namespace Carrot.Cli.Reporting;

/**************************************************************/
/// <summary>Carries one categorized iSearch batch and its approved Excel destination.</summary>
/// <seealso cref="ICategorizedISearchResultsExporter"/>
internal sealed record SaveCategorizedISearchResultsRequest
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the categorized iSearch batch to write.</summary>
    public required CategorizedISearchResultBatch Batch { get; init; }

    /**************************************************************/
    /// <summary>Gets the normalized absolute workbook destination.</summary>
    public required string OutputPath { get; init; }

    /**************************************************************/
    /// <summary>Gets whether an existing workbook may be replaced.</summary>
    public bool Overwrite { get; init; }

    #endregion
}
