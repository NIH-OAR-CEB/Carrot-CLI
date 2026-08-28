using Carrot.Cli.Input;

namespace Carrot.Cli.Extraction;

/**************************************************************/
/// <summary>
/// Contains searchable text and stable correlation data for one successfully extracted source.
/// </summary>
/// <seealso cref="SourceFile"/>
internal sealed record ExtractedDocument
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the original source descriptor.</summary>
    public required SourceFile SourceFile { get; init; }

    /**************************************************************/
    /// <summary>Gets the zero-based Carrot document index assigned after successful extraction.</summary>
    public int CarrotDocumentIndex { get; init; }

    /**************************************************************/
    /// <summary>Gets the title field supplied to Carrot.</summary>
    public required string Title { get; init; }

    /**************************************************************/
    /// <summary>Gets the complete searchable content supplied to Carrot.</summary>
    public required string Content { get; init; }

    /**************************************************************/
    /// <summary>Gets the source-file SHA-256 digest in uppercase hexadecimal form.</summary>
    public required string Sha256 { get; init; }

    #endregion
}
