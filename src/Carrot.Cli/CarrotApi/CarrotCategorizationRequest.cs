using Carrot.Cli.Configuration;
using Carrot.Cli.CarrotApi.Contracts;

namespace Carrot.Cli.CarrotApi;

/**************************************************************/
/// <summary>Describes one ordered source-neutral Carrot categorization request.</summary>
/// <remarks>
/// Source adapters provide the exact document fields while this contract keeps endpoint,
/// clustering-selection, and timeout policy in one shared Carrot execution boundary.
/// </remarks>
/// <seealso cref="CarrotCategorizer"/>
/// <seealso cref="ClusterDocument"/>
internal sealed record CarrotCategorizationRequest
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the validated absolute Carrot service endpoint.</summary>
    public required Uri Endpoint { get; init; }

    /**************************************************************/
    /// <summary>Gets the ordered documents that will become the Carrot request array.</summary>
    public IReadOnlyList<ClusterDocument> Documents { get; init; } = Array.Empty<ClusterDocument>();

    /**************************************************************/
    /// <summary>Gets optional algorithm, language, template, and parameter-file selections.</summary>
    public ClusteringSelection Clustering { get; init; } = new();

    /**************************************************************/
    /// <summary>Gets the overall time budget applied independently to each Carrot operation.</summary>
    public required TimeSpan Timeout { get; init; }

    #endregion
}
