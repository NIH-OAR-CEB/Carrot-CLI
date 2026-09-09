using Carrot.Cli.Configuration;
using Carrot.Cli.ISearch;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>Defines bounded terminal presentation for generic iSearch records.</summary>
/// <remarks>Implementations must keep records literal so arbitrary JSON cannot be interpreted as console markup.</remarks>
/// <seealso cref="SearchResultsPager"/>
internal interface ISearchResultsPager
{
    /**************************************************************/
    /// <summary>Displays configured cardinality, result records, and two-layer page navigation.</summary>
    /// <param name="session">The continuation-aware session for the current iSearch query.</param>
    /// <param name="cardinalityFieldNames">The configured labels for the common cardinality values.</param>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task representing results navigation.</returns>
    Task ShowAsync(
        SearchResultPageSession session,
        SearchCardinalityFieldNames cardinalityFieldNames,
        CancellationToken cancellationToken);
}
