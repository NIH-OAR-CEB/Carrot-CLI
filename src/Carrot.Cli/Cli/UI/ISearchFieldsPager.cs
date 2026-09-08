using Carrot.Cli.ISearch.Contracts;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>Defines bounded terminal presentation for discovered iSearch fields.</summary>
/// <remarks>Implementations must keep service-provided field values literal and preserve the supplied field objects.</remarks>
/// <seealso cref="SearchFieldsPager"/>
/// <seealso cref="SearchField"/>
internal interface ISearchFieldsPager
{
    /**************************************************************/
    /// <summary>Displays sorted field metadata until the operator goes back.</summary>
    /// <param name="fields">The validated field definitions returned by iSearch.</param>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task representing field navigation.</returns>
    Task ShowAsync(IReadOnlyList<SearchField> fields, CancellationToken cancellationToken);
}
