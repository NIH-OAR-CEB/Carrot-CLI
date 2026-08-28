using Carrot.Cli.Common;
using Carrot.Cli.Input;

namespace Carrot.Cli.Extraction;

/**************************************************************/
/// <summary>
/// Preserves one source file and its success, partial-success, or expected extraction failure.
/// </summary>
/// <seealso cref="OperationResult{T}"/>
internal sealed record ExtractionResult
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the source descriptor retained even when extraction fails.</summary>
    public required SourceFile SourceFile { get; init; }

    /**************************************************************/
    /// <summary>Gets the structured extraction outcome.</summary>
    public required OperationResult<ExtractedDocument> Outcome { get; init; }

    #endregion
}
