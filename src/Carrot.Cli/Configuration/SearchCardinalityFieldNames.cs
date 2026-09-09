namespace Carrot.Cli.Configuration;

/**************************************************************/
/// <summary>
/// Defines the configured report names for common iSearch result-cardinality values.
/// </summary>
/// <remarks>
/// These values label the CLI report and are not sent to iSearch as <c>fl</c> record fields. The
/// service response remains responsible for supplying the count values and cursor.
/// </remarks>
/// <seealso cref="SearchReturnTypeConfiguration"/>
/// <seealso cref="Carrot.Cli.ISearch.Contracts.SearchCardinality"/>
internal sealed class SearchCardinalityFieldNames
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the report name for the total number of matching results.</summary>
    public string TotalResultsFieldName { get; init; } = string.Empty;

    /**************************************************************/
    /// <summary>Gets the report name for the results in the current response.</summary>
    public string CurrentResultsFieldName { get; init; } = string.Empty;

    /**************************************************************/
    /// <summary>Gets the report name for the current result page number.</summary>
    public string PageNumberFieldName { get; init; } = string.Empty;

    /**************************************************************/
    /// <summary>Gets the report name for the total number of result pages.</summary>
    public string TotalPagesFieldName { get; init; } = string.Empty;

    #endregion
}
