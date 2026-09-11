using Carrot.Cli.Common;

namespace Carrot.Cli.ISearch;

/**************************************************************/
/// <summary>Defines prompt-free iSearch command orchestration.</summary>
/// <seealso cref="SearchCommandWorkflow"/>
internal interface ISearchCommandWorkflow
{
    /**************************************************************/
    /// <summary>Runs one validated named search and its requested downstream operations.</summary>
    /// <param name="request">The parsed command values.</param>
    /// <param name="cancellationToken">The token that cancels service, paging, categorization, or output work.</param>
    /// <returns>The completed, partial, or failed named search result.</returns>
    Task<OperationResult<SearchCommandResult>> RunAsync(
        SearchCommandRequest request,
        CancellationToken cancellationToken);
}
