using Carrot.Cli.Configuration;

namespace Carrot.Cli.Processing;

/**************************************************************/
/// <summary>
/// Carries one fully resolved discovery, extraction, and request-serialization preview.
/// </summary>
/// <seealso cref="IDocumentProcessingWorkflow.PreviewAsync"/>
internal sealed record PreviewRequest
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the folder or ZIP archive input path.</summary>
    public required string InputPath { get; init; }

    /**************************************************************/
    /// <summary>Gets the absolute Carrot service base URI used for configuration validation.</summary>
    public required Uri Endpoint { get; init; }

    /**************************************************************/
    /// <summary>Gets the preview artifact output path.</summary>
    public string? OutputPath { get; init; }

    /**************************************************************/
    /// <summary>Gets whether folder discovery includes subdirectories.</summary>
    public bool Recursive { get; init; }

    /**************************************************************/
    /// <summary>Gets the normalized clustering selection resolved later into request values.</summary>
    public required ClusteringSelection Clustering { get; init; }

    /**************************************************************/
    /// <summary>Gets the resolved HTTP timeout used for list validation.</summary>
    public TimeSpan Timeout { get; init; }

    /**************************************************************/
    /// <summary>Gets whether an existing preview artifact may be replaced.</summary>
    public bool Overwrite { get; init; }

    #endregion
}
