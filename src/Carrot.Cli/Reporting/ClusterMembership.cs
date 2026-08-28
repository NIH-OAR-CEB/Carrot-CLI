namespace Carrot.Cli.Reporting;

/**************************************************************/
/// <summary>
/// Preserves one exact recursive cluster membership for one submitted document index.
/// </summary>
internal sealed record ClusterMembership
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the zero-based submitted Carrot document index.</summary>
    public int CarrotDocumentIndex { get; init; }

    /**************************************************************/
    /// <summary>Gets labels on the matching node in server-returned order.</summary>
    public IReadOnlyList<string> Labels { get; init; } = Array.Empty<string>();

    /**************************************************************/
    /// <summary>Gets the flattened full label path from root to matching node.</summary>
    public required string CategoryPath { get; init; }

    /**************************************************************/
    /// <summary>Gets the numeric score associated with the matching node.</summary>
    public double? Score { get; init; }

    /**************************************************************/
    /// <summary>Gets the zero-based nesting depth of the matching node.</summary>
    public int Depth { get; init; }

    #endregion
}
