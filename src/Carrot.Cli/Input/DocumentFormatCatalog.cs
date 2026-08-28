namespace Carrot.Cli.Input;

/**************************************************************/
/// <summary>
/// Provides the authoritative case-insensitive set of document extensions accepted by preparation.
/// </summary>
/// <remarks>
/// Input validation, folder and ZIP discovery, and extraction strategy resolution share this
/// catalog so a format cannot be accepted by one subsystem and rejected by another.
/// </remarks>
internal sealed class DocumentFormatCatalog
{
    #region implementation

    private static readonly IReadOnlySet<string> Extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".docx",
        ".xlsx",
        ".pptx",
        ".txt",
        ".md",
        ".pdf"
    };

    /**************************************************************/
    /// <summary>Gets all supported extensions including their leading periods.</summary>
    internal IReadOnlySet<string> SupportedExtensions => Extensions;

    /**************************************************************/
    /// <summary>Determines whether a path has a supported document extension.</summary>
    /// <param name="path">The file or archive-entry path to inspect.</param>
    /// <returns><see langword="true"/> when the extension is supported; otherwise <see langword="false"/>.</returns>
    internal bool IsSupportedDocument(string path)
    {
        #region implementation

        return !string.IsNullOrWhiteSpace(path) && Extensions.Contains(Path.GetExtension(path));

        #endregion
    }

    /**************************************************************/
    /// <summary>Determines whether a discovered path is an Office temporary owner file.</summary>
    /// <param name="path">The source path to inspect.</param>
    /// <returns><see langword="true"/> when the filename begins with <c>~$</c>.</returns>
    internal static bool IsOfficeTemporaryFile(string path)
    {
        #region implementation

        return Path.GetFileName(path).StartsWith("~$", StringComparison.Ordinal);

        #endregion
    }

    #endregion
}
