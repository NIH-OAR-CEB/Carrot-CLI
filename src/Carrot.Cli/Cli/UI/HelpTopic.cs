namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>
/// Describes one curated help topic, its embedded Markdown resource, and accepted aliases.
/// </summary>
/// <remarks>
/// Topic metadata is defined by <see cref="HelpTopicCatalog"/> and is shared by the
/// interactive help menu and the named <c>help</c> command.
/// </remarks>
/// <seealso cref="HelpTopicCatalog"/>
/// <seealso cref="IHelpContentProvider"/>
internal sealed record HelpTopic
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the canonical command-line key for the topic.</summary>
    public required string Key { get; init; }

    /**************************************************************/
    /// <summary>Gets the human-readable title displayed in the interactive topic menu.</summary>
    public required string Title { get; init; }

    /**************************************************************/
    /// <summary>Gets the logical manifest-resource name containing the topic's Markdown.</summary>
    public required string ResourceName { get; init; }

    /**************************************************************/
    /// <summary>Gets additional case-insensitive names accepted by the named help command.</summary>
    public IReadOnlyList<string> Aliases { get; init; } = Array.Empty<string>();

    #endregion
}
