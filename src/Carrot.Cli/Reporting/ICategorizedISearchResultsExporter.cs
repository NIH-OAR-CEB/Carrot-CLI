using Carrot.Cli.Common;

namespace Carrot.Cli.Reporting;

/**************************************************************/
/// <summary>Defines explicit persistence of a categorized iSearch batch to Excel.</summary>
/// <seealso cref="CategorizedISearchResultsExporter"/>
internal interface ICategorizedISearchResultsExporter
{
    /**************************************************************/
    /// <summary>Maps and saves categorized iSearch data without contacting either service.</summary>
    /// <param name="request">The categorized batch, destination, and overwrite policy.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>The absolute saved path or a structured expected failure.</returns>
    Task<OperationResult<string>> SaveAsync(
        SaveCategorizedISearchResultsRequest request,
        CancellationToken cancellationToken);
}
