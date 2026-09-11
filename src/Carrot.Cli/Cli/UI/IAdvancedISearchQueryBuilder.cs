using Carrot.Cli.Configuration;
using Carrot.Cli.ISearch.Contracts;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>Builds an operator-reviewed advanced iSearch request from live field metadata.</summary>
/// <remarks>
/// Implementations own prompt navigation and draft validation, but do not perform network work.
/// Returning <see langword="null"/> means the operator cancelled before a request was submitted.
/// </remarks>
/// <seealso cref="AdvancedISearchQueryBuilder"/>
/// <seealso cref="SearchRequest"/>
internal interface IAdvancedISearchQueryBuilder
{
    /**************************************************************/
    /// <summary>Prompts for, previews, and optionally confirms one advanced search request.</summary>
    /// <param name="database">The exact live database selected from iSearch discovery.</param>
    /// <param name="returnType">The configured result-field set for the request.</param>
    /// <param name="fields">The live field metadata used to constrain field selections.</param>
    /// <param name="cancellationToken">The token that cancels prompt interaction.</param>
    /// <returns>The confirmed request, or <see langword="null"/> when the operator cancels.</returns>
    /// <seealso cref="SearchField"/>
    Task<SearchRequest?> BuildAsync(
        string database,
        SearchReturnTypeDefinition returnType,
        IReadOnlyList<SearchField> fields,
        CancellationToken cancellationToken);
}
