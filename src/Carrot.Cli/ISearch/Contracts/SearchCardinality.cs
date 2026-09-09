namespace Carrot.Cli.ISearch.Contracts;

/**************************************************************/
/// <summary>
/// Reports the bounded iSearch result cardinality needed to understand a dataset walk.
/// </summary>
/// <remarks>
/// Counts describe the service response page, not the number of terminal lines used to display
/// serialized records. A nonempty initial response is page one; an empty response is page zero of
/// zero pages so a caller can terminate a walk without inventing an empty page.
/// </remarks>
/// <seealso cref="SearchResponse"/>
internal sealed class SearchCardinality
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the total number of records matching the submitted query.</summary>
    public int TotalResults { get; init; }

    /**************************************************************/
    /// <summary>Gets the number of records included in the current service response.</summary>
    public int CurrentResults { get; init; }

    /**************************************************************/
    /// <summary>Gets the one-based current service result page, or zero when no page exists.</summary>
    public int PageNumber { get; init; }

    /**************************************************************/
    /// <summary>Gets the total service result pages for the request row limit.</summary>
    public int TotalPages { get; init; }

    #endregion
}
