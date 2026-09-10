using Carrot.Cli.Common;

namespace Carrot.Cli.ISearch;

/**************************************************************/
/// <summary>Defines categorization of the records retained by one iSearch session.</summary>
/// <remarks>
/// The operation maps only the selected iSearch fields and delegates all Carrot HTTP and response
/// handling to the shared categorization boundary.
/// </remarks>
/// <seealso cref="SearchResultsCategorizer"/>
internal interface ISearchResultsCategorizer
{
    /**************************************************************/
    /// <summary>Submits the loaded iSearch records to Carrot in their retained order.</summary>
    /// <param name="session">The current iSearch session and loaded result pages.</param>
    /// <param name="endpoint">The normalized Carrot service endpoint.</param>
    /// <param name="timeout">The timeout applied to each Carrot operation.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>The correlated categorized batch or a structured expected failure.</returns>
    Task<OperationResult<CategorizedISearchResultBatch>> CategorizeAsync(
        SearchResultPageSession session,
        Uri endpoint,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
