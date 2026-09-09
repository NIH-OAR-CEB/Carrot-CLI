using Carrot.Cli.Common;

namespace Carrot.Cli.Reporting;

/**************************************************************/
/// <summary>Defines explicit persistence of all pages walked in one iSearch session.</summary>
/// <seealso cref="SearchResultsExporter"/>
internal interface ISearchResultsExporter
{
    /**************************************************************/
    /// <summary>Maps and saves the retained iSearch session without contacting the service.</summary>
    /// <param name="request">The retained session, destination, and overwrite policy.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>The saved absolute path or a structured expected failure.</returns>
    Task<OperationResult<string>> SaveAsync(
        SaveISearchResultsRequest request,
        CancellationToken cancellationToken);
}
