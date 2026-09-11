using Carrot.Cli.Common;

namespace Carrot.Cli.ISearch;

/**************************************************************/
/// <summary>Describes one named iSearch run and its accepted records and artifacts.</summary>
/// <remarks>The session remains available to exporters and categorization only for the current invocation.</remarks>
/// <seealso cref="SearchResultPageSession"/>
internal sealed record SearchCommandResult
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the live database used by the search.</summary>
    public required string Database { get; init; }

    /**************************************************************/
    /// <summary>Gets the configured return-dataset label used for fl.</summary>
    public required string ReturnDataset { get; init; }

    /**************************************************************/
    /// <summary>Gets the retained search session.</summary>
    public required SearchResultPageSession Session { get; init; }

    /**************************************************************/
    /// <summary>Gets the categorized batch when categorization succeeded.</summary>
    public CategorizedISearchResultBatch? CategorizedBatch { get; init; }

    /**************************************************************/
    /// <summary>Gets the successfully written absolute artifact paths.</summary>
    public IReadOnlyList<string> ArtifactPaths { get; init; } = Array.Empty<string>();

    /**************************************************************/
    /// <summary>Gets whether the retained session reached the service total.</summary>
    public bool IsComplete { get; init; }

    /**************************************************************/
    /// <summary>Gets whether Carrot categorization succeeded.</summary>
    public bool WasCategorized => CategorizedBatch is not null;

    #endregion
}
