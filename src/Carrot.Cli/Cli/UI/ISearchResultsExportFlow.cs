using Carrot.Cli.ISearch;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>Defines the interactive path and confirmation flow for iSearch result export.</summary>
/// <seealso cref="SearchResultsExportFlow"/>
internal interface ISearchResultsExportFlow
{
    /**************************************************************/
    /// <summary>Prompts for a destination and saves the supplied retained session when approved.</summary>
    /// <param name="session">The iSearch session containing all walked pages.</param>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task representing the complete interaction.</returns>
    Task RunAsync(SearchResultPageSession session, CancellationToken cancellationToken);
}
