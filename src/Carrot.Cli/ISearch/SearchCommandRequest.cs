using Carrot.Cli.ISearch.Contracts;

namespace Carrot.Cli.ISearch;

/**************************************************************/
/// <summary>Carries parsed named-command values before live iSearch resolution.</summary>
/// <remarks>The workflow resolves configured fields, endpoint/path values, live field references, and service state.</remarks>
/// <seealso cref="ISearchCommandWorkflow"/>
internal sealed record SearchCommandRequest
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the requested live iSearch database.</summary>
    public required string Database { get; init; }

    /**************************************************************/
    /// <summary>Gets the configured return-dataset name.</summary>
    public required string ReturnDataset { get; init; }

    /**************************************************************/
    /// <summary>Gets the base query, including the match-all default.</summary>
    public required string Query { get; init; }

    /**************************************************************/
    /// <summary>Gets ordered query fields for unqualified terms.</summary>
    public IReadOnlyList<string> QueryFields { get; init; } = Array.Empty<string>();

    /**************************************************************/
    /// <summary>Gets ordered complete field-qualified filter expressions.</summary>
    public IReadOnlyList<string> FilterQueries { get; init; } = Array.Empty<string>();

    /**************************************************************/
    /// <summary>Gets the default Boolean operator.</summary>
    public required string DefaultOp { get; init; }

    /**************************************************************/
    /// <summary>Gets the service page row limit.</summary>
    public required int Rows { get; init; }

    /**************************************************************/
    /// <summary>Gets the optional lower update-date bound.</summary>
    public string? UpdatedAfter { get; init; }

    /**************************************************************/
    /// <summary>Gets the optional upper update-date bound.</summary>
    public string? UpdatedBefore { get; init; }

    /**************************************************************/
    /// <summary>Gets the optional retained-record bound.</summary>
    public int? MaxResults { get; init; }

    /**************************************************************/
    /// <summary>Gets whether the workflow must walk to the service-reported total.</summary>
    public bool AllResults { get; init; }

    /**************************************************************/
    /// <summary>Gets the optional original workbook path.</summary>
    public string? OutputPath { get; init; }

    /**************************************************************/
    /// <summary>Gets whether Carrot categorization is requested.</summary>
    public bool Categorize { get; init; }

    /**************************************************************/
    /// <summary>Gets the optional categorized workbook path.</summary>
    public string? CategorizedOutputPath { get; init; }

    /**************************************************************/
    /// <summary>Gets the optional Carrot endpoint override.</summary>
    public string? Endpoint { get; init; }

    /**************************************************************/
    /// <summary>Gets the optional Carrot timeout override in seconds.</summary>
    public int? TimeoutSeconds { get; init; }

    /**************************************************************/
    /// <summary>Gets whether existing workbooks may be overwritten.</summary>
    public bool Overwrite { get; init; }

    #endregion
}
