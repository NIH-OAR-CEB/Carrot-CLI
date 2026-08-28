namespace Carrot.Cli.Input;

/**************************************************************/
/// <summary>
/// Owns the ordered source files and any temporary directory created for one input container.
/// </summary>
/// <remarks>
/// Disposal is the lifecycle boundary for deleting temporary ZIP extraction content.
/// </remarks>
/// <seealso cref="SourceFile"/>
internal sealed class InputBatch : IAsyncDisposable
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the original folder or ZIP container path.</summary>
    public required string ContainerPath { get; init; }

    /**************************************************************/
    /// <summary>Gets the deterministic source-file sequence.</summary>
    public IReadOnlyList<SourceFile> Files { get; init; } = Array.Empty<SourceFile>();

    /**************************************************************/
    /// <summary>Gets the owned temporary extraction directory, when the input was a ZIP archive.</summary>
    public string? TemporaryDirectoryPath { get; init; }

    /**************************************************************/
    /// <summary>
    /// Releases any temporary extraction directory owned by the batch.
    /// </summary>
    /// <returns>A value task representing asynchronous cleanup.</returns>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    public ValueTask DisposeAsync()
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
