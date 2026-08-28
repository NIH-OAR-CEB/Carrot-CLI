namespace Carrot.Cli.Processing;

/**************************************************************/
/// <summary>Carries the ordered interactive paths and shared folder-recursion choice for preparation.</summary>
/// <seealso cref="IDocumentPreparationWorkflow.PrepareAsync"/>
internal sealed record PrepareDocumentsRequest
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the user-entered file, folder, and ZIP paths in batch order.</summary>
    public IReadOnlyList<string> InputPaths { get; init; } = Array.Empty<string>();

    /**************************************************************/
    /// <summary>Gets whether discovery includes subdirectories for every folder input.</summary>
    public bool Recursive { get; init; }

    #endregion
}
