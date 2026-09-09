namespace Carrot.Cli.Configuration;

/**************************************************************/
/// <summary>
/// Contains shared result-report settings and the ordered configured iSearch return types.
/// </summary>
/// <remarks>
/// The <c>Results</c> configuration section owns one cardinality vocabulary shared by every
/// child return type, such as <c>Grants</c>. Return-type field lists remain dataset-specific.
/// </remarks>
/// <seealso cref="SearchCardinalityFieldNames"/>
/// <seealso cref="SearchReturnTypeDefinition"/>
internal sealed class SearchReturnTypeConfiguration
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the shared report names used for result cardinality.</summary>
    /// <remarks>The names apply uniformly to every configured return type.</remarks>
    public SearchCardinalityFieldNames Cardinality { get; init; } = new();

    /**************************************************************/
    /// <summary>Gets the ordered configured return types available to the interactive workflow.</summary>
    /// <remarks>The order is preserved from the <c>Results</c> configuration section.</remarks>
    public IReadOnlyList<SearchReturnTypeDefinition> ReturnTypes { get; init; } = Array.Empty<SearchReturnTypeDefinition>();

    #endregion
}
