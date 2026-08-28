namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>
/// Loads the Markdown content associated with a curated help topic.
/// </summary>
/// <seealso cref="EmbeddedHelpContentProvider"/>
/// <seealso cref="HelpTopic"/>
internal interface IHelpContentProvider
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Reads a topic's Markdown content from its configured storage boundary.
    /// </summary>
    /// <param name="topic">The resolved topic whose content is required.</param>
    /// <returns>The Markdown content, or <see langword="null"/> when the resource is unavailable.</returns>
    string? Read(HelpTopic topic);

    #endregion
}
