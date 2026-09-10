using Carrot.Cli.ISearch.Contracts;

namespace Carrot.Cli.ISearch;

/**************************************************************/
/// <summary>Describes one committed progress snapshot from an iSearch service-page walk.</summary>
/// <remarks>
/// The session creates a new snapshot before the walk begins and after each response is validated and
/// appended. Percentage is based on records accepted into the current session divided by the stable
/// service total, rather than on page numbers. This preserves accurate progress when the final service
/// page contains fewer records than the configured row limit. The snapshot is an internal UI contract;
/// it does not own or mutate the records retained by <see cref="SearchResultPageSession"/>.
/// </remarks>
/// <seealso cref="SearchResultPageSession"/>
/// <seealso cref="SearchResponse"/>
internal sealed class SearchPageWalkProgress
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the current one-based service data page, or zero for an empty result.</summary>
    /// <remarks>
    /// This is the service page represented by the latest accepted response, not the terminal display
    /// page selected by the operator when a record spans multiple rendered lines.
    /// </remarks>
    public required int DataPage { get; init; }

    /**************************************************************/
    /// <summary>Gets the total service data pages calculated for the query.</summary>
    /// <remarks>The value comes from iSearch cardinality and is contextual status information only.</remarks>
    public required int TotalDataPages { get; init; }

    /**************************************************************/
    /// <summary>Gets the number of records accepted into the session aggregate.</summary>
    /// <remarks>The count includes the initial response and every later response committed in order.</remarks>
    public required int LoadedRecords { get; init; }

    /**************************************************************/
    /// <summary>Gets the stable total number of records reported by the initial response.</summary>
    /// <remarks>A later response that changes this denominator is rejected before it can alter the session.</remarks>
    public required int TotalRecords { get; init; }

    /**************************************************************/
    /// <summary>Gets the record-based loaded percentage in the inclusive range 0 through 100.</summary>
    /// <remarks>
    /// Empty result sets report 100 percent. Nonempty walks are calculated from loaded records and are
    /// clamped so malformed or over-complete service counts cannot produce a value outside the range.
    /// </remarks>
    public required double Percentage { get; init; }

    /**************************************************************/
    /// <summary>Gets whether the session has loaded the complete service result set.</summary>
    /// <remarks>
    /// Completion means the stable total has been accepted, including the valid zero-record result case;
    /// it does not mean that the latest response merely supplied a continuation cursor.
    /// </remarks>
    public required bool IsComplete { get; init; }

    #endregion
}
