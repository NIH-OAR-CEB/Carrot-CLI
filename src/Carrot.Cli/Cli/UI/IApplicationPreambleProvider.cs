namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>
/// Loads the editable Markdown displayed before the interactive main menu.
/// </summary>
/// <remarks>
/// The preamble remains external to the assembly so deployed text can be changed without
/// rebuilding the application. Implementations should read current content on every call.
/// </remarks>
/// <seealso cref="FileApplicationPreambleProvider"/>
/// <seealso cref="ApplicationPreambleRenderer"/>
internal interface IApplicationPreambleProvider
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Reads the current application preamble Markdown from its configured source.
    /// </summary>
    /// <returns>The current Markdown, or <see langword="null"/> when it cannot be read.</returns>
    string? Read();

    #endregion
}
