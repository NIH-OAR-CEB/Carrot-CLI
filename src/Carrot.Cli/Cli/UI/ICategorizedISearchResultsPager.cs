using Carrot.Cli.ISearch;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>Displays the successful categorized iSearch result batch.</summary>
/// <remarks>The abstraction keeps the source-result pager independent from the categorized display implementation.</remarks>
/// <seealso cref="CategorizedISearchResultBatch"/>
internal interface ICategorizedISearchResultsPager
{
    /**************************************************************/
    /// <summary>Runs categorized-result navigation until the operator returns.</summary>
    /// <param name="batch">The successful categorized iSearch batch.</param>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task representing categorized-result navigation.</returns>
    Task ShowAsync(CategorizedISearchResultBatch batch, CancellationToken cancellationToken);
}
