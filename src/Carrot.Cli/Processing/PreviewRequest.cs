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
    /// <summary>Gets the resolved clustering algorithm identifier.</summary>
    public string? Algorithm { get; init; }

    /**************************************************************/
    /// <summary>Gets the resolved clustering language identifier.</summary>
    public string? Language { get; init; }

    /**************************************************************/
    /// <summary>Gets the optional server-side template identifier.</summary>
    public string? Template { get; init; }

    /**************************************************************/
    /// <summary>Gets the optional algorithm-parameter JSON file path.</summary>
    public string? ParametersFile { get; init; }

    /**************************************************************/
    /// <summary>Gets the resolved HTTP timeout used for list validation.</summary>
    public TimeSpan Timeout { get; init; }

    /**************************************************************/
    /// <summary>Gets whether an existing preview artifact may be replaced.</summary>
    public bool Overwrite { get; init; }

    #endregion
}
