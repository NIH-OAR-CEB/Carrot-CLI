using Carrot.Cli.ISearch;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>Defines the interactive Excel flow for categorized iSearch results.</summary>
/// <seealso cref="CategorizedISearchResultsExportFlow"/>
internal interface ICategorizedISearchResultsExportFlow
{
    /**************************************************************/
    /// <summary>Prompts for a destination and saves the supplied categorized batch.</summary>
    /// <param name="batch">The completed categorized iSearch batch.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>A task representing the complete export interaction.</returns>
    Task RunAsync(CategorizedISearchResultBatch batch, CancellationToken cancellationToken);
}
