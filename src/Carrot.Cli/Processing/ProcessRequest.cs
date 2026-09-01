using Carrot.Cli.Configuration;

namespace Carrot.Cli.Processing;

/**************************************************************/
/// <summary>
/// Carries one fully resolved, noninteractive end-to-end processing request.
/// </summary>
/// <seealso cref="IDocumentProcessingWorkflow.ProcessAsync"/>
internal sealed record ProcessRequest
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the folder or ZIP archive input path.</summary>
    public required string InputPath { get; init; }

    /**************************************************************/
    /// <summary>Gets the absolute Carrot service base URI.</summary>
    public required Uri Endpoint { get; init; }

    /**************************************************************/
    /// <summary>Gets the optional explicit output path.</summary>
    public string? OutputPath { get; init; }

    /**************************************************************/
    /// <summary>Gets whether folder discovery includes subdirectories.</summary>
    public bool Recursive { get; init; }

    /**************************************************************/
    /// <summary>Gets the normalized clustering selection resolved later into request values.</summary>
    public required ClusteringSelection Clustering { get; init; }

    /**************************************************************/
    /// <summary>Gets the resolved HTTP timeout.</summary>
    public TimeSpan Timeout { get; init; }

    /**************************************************************/
    /// <summary>Gets whether existing artifacts may be replaced.</summary>
    public bool Overwrite { get; init; }

    /**************************************************************/
    /// <summary>Gets whether JSON request and response sidecars are suppressed.</summary>
    public bool WriteJsonArtifacts { get; init; } = true;

    /**************************************************************/
    /// <summary>Gets whether normal console output is suppressed.</summary>
    public bool Quiet { get; init; }

    /**************************************************************/
    /// <summary>Gets the optional explicit diagnostic log path.</summary>
    public string? LogFile { get; init; }

    #endregion
}
