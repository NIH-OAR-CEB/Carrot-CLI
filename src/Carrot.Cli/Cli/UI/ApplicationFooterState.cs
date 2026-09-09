namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>Describes the application state presented by the shared interactive footer.</summary>
/// <remarks>
/// Result-page and display-page values are intentionally separate. The former identifies a
/// service data chunk, while the latter identifies terminal lines already held in memory.
/// </remarks>
internal sealed class ApplicationFooterState
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the interactive context represented by the footer.</summary>
    public required string Context { get; init; }

    /**************************************************************/
    /// <summary>Gets the live dataset selected for the current operation, when applicable.</summary>
    public string? Dataset { get; init; }

    /**************************************************************/
    /// <summary>Gets the configured return dataset selected for the current operation, when applicable.</summary>
    public string? ReturnDataset { get; init; }

    /**************************************************************/
    /// <summary>Gets the current terminal display page, when a result view is active.</summary>
    public int? DisplayPage { get; init; }

    /**************************************************************/
    /// <summary>Gets the terminal display-page count, when a result view is active.</summary>
    public int? DisplayPageCount { get; init; }

    /**************************************************************/
    /// <summary>Gets the current service data page, when a result view is active.</summary>
    public int? ResultPage { get; init; }

    /**************************************************************/
    /// <summary>Gets the service data-page count, when a result view is active.</summary>
    public int? ResultPageCount { get; init; }

    /**************************************************************/
    /// <summary>Gets the number of records in the current service response, when available.</summary>
    public int? CurrentResults { get; init; }

    /**************************************************************/
    /// <summary>Gets the total number of matching records, when available.</summary>
    public int? TotalResults { get; init; }

    /**************************************************************/
    /// <summary>Gets whether another service data page can be fetched, when a result view is active.</summary>
    public bool? CanFetchNextResultPage { get; init; }

    /**************************************************************/
    /// <summary>Gets concise interaction guidance displayed below the current content.</summary>
    public required string Instruction { get; init; }

    #endregion
}
