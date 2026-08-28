using System.Text;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>
/// Reads the editable application preamble from the deployment's content directory.
/// </summary>
/// <remarks>
/// The provider intentionally performs no caching. Updating
/// <c>Content/application-preamble.md</c> changes the preamble shown by the next process
/// launch without recompiling or republishing the application.
/// </remarks>
/// <seealso cref="IApplicationPreambleProvider"/>
internal sealed class FileApplicationPreambleProvider : IApplicationPreambleProvider
{
    #region implementation

    private readonly string _preamblePath;

    /**************************************************************/
    /// <summary>
    /// Initializes the provider with the standard application-relative preamble path.
    /// </summary>
    public FileApplicationPreambleProvider()
        : this(Path.Combine(AppContext.BaseDirectory, "Content", "application-preamble.md"))
    {
        #region implementation

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Initializes the provider with an explicit path for deterministic file-boundary tests.
    /// </summary>
    /// <param name="preamblePath">The absolute or test-controlled Markdown file path.</param>
    /// <exception cref="ArgumentException">Thrown when the path is empty or whitespace.</exception>
    internal FileApplicationPreambleProvider(string preamblePath)
    {
        #region implementation

        ArgumentException.ThrowIfNullOrWhiteSpace(preamblePath);
        _preamblePath = preamblePath;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Reads the current UTF-8 preamble text without retaining a cached copy.
    /// </summary>
    /// <returns>The current Markdown, or <see langword="null"/> when the file is unavailable.</returns>
    public string? Read()
    {
        #region implementation

        try
        {
            return File.ReadAllText(_preamblePath, Encoding.UTF8);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or System.Security.SecurityException)
        {
            // A missing or inaccessible welcome file must not prevent access to the CLI menu.
            return null;
        }

        #endregion
    }

    #endregion
}
