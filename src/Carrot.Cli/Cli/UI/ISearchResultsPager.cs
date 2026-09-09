using Carrot.Cli.Configuration;
using Carrot.Cli.ISearch.Contracts;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>Defines bounded terminal presentation for generic iSearch records.</summary>
/// <remarks>Implementations must keep records literal so arbitrary JSON cannot be interpreted as console markup.</remarks>
/// <seealso cref="SearchResultsPager"/>
internal interface ISearchResultsPager
{
    /**************************************************************/
    /// <summary>Displays configured cardinality, result records, and bounded terminal navigation.</summary>
    /// <param name="response">The validated search response.</param>
    /// <param name="cardinalityFieldNames">The configured labels for the common cardinality values.</param>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task representing results navigation.</returns>
    Task ShowAsync(
        SearchResponse response,
        SearchCardinalityFieldNames cardinalityFieldNames,
        CancellationToken cancellationToken);
}
