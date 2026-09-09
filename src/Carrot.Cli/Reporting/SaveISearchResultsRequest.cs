using Carrot.Cli.ISearch;

namespace Carrot.Cli.Reporting;

/**************************************************************/
/// <summary>Carries one retained iSearch session and its approved Excel destination.</summary>
/// <seealso cref="ISearchResultsExporter"/>
internal sealed record SaveISearchResultsRequest
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the session containing every successfully walked page.</summary>
    public required SearchResultPageSession Session { get; init; }

    /**************************************************************/
    /// <summary>Gets the normalized absolute workbook destination.</summary>
    public required string OutputPath { get; init; }

    /**************************************************************/
    /// <summary>Gets whether an existing workbook may be replaced.</summary>
    public bool Overwrite { get; init; }

    #endregion
}
