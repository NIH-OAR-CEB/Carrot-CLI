namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>Describes the application state presented by the shared interactive footer.</summary>
/// <remarks>
/// Result-page and display-page values are intentionally separate. The former identifies a
/// service data chunk, while the latter identifies terminal lines already held in memory.
/// Loaded-record values describe the complete retained service-page walk and are independent of
/// the records shown on the current terminal display page. Nullable fields allow the same immutable
/// state object to describe both the general application footer and an active iSearch result view.
/// Values are presentation snapshots; the state object does not perform network work or own records.
/// </remarks>
/// <seealso cref="ApplicationFooterRenderer"/>
internal sealed class ApplicationFooterState
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the interactive context represented by the footer.</summary>
    /// <remarks>This required label identifies the workflow whose status is being summarized.</remarks>
    public required string Context { get; init; }

    /**************************************************************/
    /// <summary>Gets the live dataset selected for the current operation, when applicable.</summary>
    /// <remarks>The value is displayed literally and omitted from the panel when no dataset is selected.</remarks>
    public string? Dataset { get; init; }

    /**************************************************************/
    /// <summary>Gets the configured return dataset selected for the current operation, when applicable.</summary>
    /// <remarks>This is the selected result shape and is distinct from the live service dataset.</remarks>
    public string? ReturnDataset { get; init; }

    /**************************************************************/
    /// <summary>Gets the current terminal display page, when a result view is active.</summary>
    /// <remarks>Display pages count rendered terminal lines and do not represent additional API requests.</remarks>
    public int? DisplayPage { get; init; }

    /**************************************************************/
    /// <summary>Gets the terminal display-page count, when a result view is active.</summary>
    /// <remarks>The count is calculated from the current response's rendered lines and configured page size.</remarks>
    public int? DisplayPageCount { get; init; }

    /**************************************************************/
    /// <summary>Gets the current service data page, when a result view is active.</summary>
    /// <remarks>This value changes when a service response is fetched and accepted.</remarks>
    public int? ResultPage { get; init; }

    /**************************************************************/
    /// <summary>Gets the service data-page count, when a result view is active.</summary>
    /// <remarks>This is service cardinality metadata, not a count of terminal display pages.</remarks>
    public int? ResultPageCount { get; init; }

    /**************************************************************/
    /// <summary>Gets the number of records in the current service response, when available.</summary>
    /// <remarks>The value describes only the current chunk and may be smaller than the retained walk.</remarks>
    public int? CurrentResults { get; init; }

    /**************************************************************/
    /// <summary>Gets the total number of matching records, when available.</summary>
    /// <remarks>For iSearch this is the stable denominator used to calculate loaded-record progress.</remarks>
    public int? TotalResults { get; init; }

    /**************************************************************/
    /// <summary>Gets the number of records accepted across all walked service pages.</summary>
    /// <remarks>
    /// The count includes every committed page in the current query visit, including the initial page,
    /// and is independent of the terminal display page currently visible.
    /// </remarks>
    public int? LoadedResults { get; init; }

    /**************************************************************/
    /// <summary>Gets the actual record-based percentage loaded across the current query visit.</summary>
    /// <remarks>
    /// The value is expected to be between 0 and 100 and is rendered as a bounded progress bar. It is
    /// based on loaded records rather than page numbers so a partial final page is not overstated.
    /// </remarks>
    public double? LoadPercentage { get; init; }

    /**************************************************************/
    /// <summary>Gets whether the all-pages service walk is currently loading data.</summary>
    /// <remarks>
    /// The renderer uses this presentation flag to highlight progress and paging values in orange
    /// during active loading, then returns them to the ordinary footer styling when the walk ends.
    /// </remarks>
    public bool IsFetchingAllPages { get; init; }

    /**************************************************************/
    /// <summary>Gets whether another service data page can be fetched, when a result view is active.</summary>
    /// <remarks>The value reflects the session cursor/cardinality state at the time this snapshot was created.</remarks>
    public bool? CanFetchNextResultPage { get; init; }

    /**************************************************************/
    /// <summary>Gets concise interaction guidance displayed below the current content.</summary>
    /// <remarks>Guidance explains the available interaction without changing the underlying session state.</remarks>
    public required string Instruction { get; init; }

    #endregion
}
