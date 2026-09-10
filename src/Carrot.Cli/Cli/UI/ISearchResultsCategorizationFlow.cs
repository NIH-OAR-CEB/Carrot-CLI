using Carrot.Cli.Common;
using Carrot.Cli.ISearch;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>Defines the interactive endpoint and categorization flow for loaded iSearch records.</summary>
/// <seealso cref="SearchResultsCategorizationFlow"/>
internal interface ISearchResultsCategorizationFlow
{
    /**************************************************************/
    /// <summary>Prompts for Carrot settings and categorizes the supplied iSearch session.</summary>
    /// <param name="session">The session containing records already loaded from iSearch.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>The categorized batch or a structured expected failure.</returns>
    Task<OperationResult<CategorizedISearchResultBatch>> RunAsync(
        SearchResultPageSession session,
        CancellationToken cancellationToken);
}
