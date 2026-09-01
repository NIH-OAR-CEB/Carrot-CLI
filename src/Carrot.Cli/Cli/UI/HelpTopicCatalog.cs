using System.Diagnostics.CodeAnalysis;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>
/// Owns the immutable, ordered catalog of user-selectable Carrot CLI help topics.
/// </summary>
/// <remarks>
/// The catalog centralizes topic keys, labels, aliases, and resources so interactive and
/// noninteractive help cannot drift apart. It is safe to register as a singleton.
/// </remarks>
/// <seealso cref="HelpTopic"/>
/// <seealso cref="HelpRenderer"/>
internal sealed class HelpTopicCatalog
{
    #region implementation

    private const string ResourcePrefix = "Carrot.Cli.Help.";
    private readonly IReadOnlyDictionary<string, HelpTopic> _topicsByKey;

    /**************************************************************/
    /// <summary>
    /// Initializes the ordered topic collection and its normalized alias lookup.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when two configured topics claim the same normalized key or alias.
    /// </exception>
    public HelpTopicCatalog()
    {
        #region implementation

        Topics = new[]
        {
            createTopic("getting-started", "Getting Started", "start", "overview"),
            createTopic("task-scheduler", "Windows Task Scheduler", "scheduler", "scheduling"),
            createTopic("process", "Process Documents"),
            createTopic("preview", "Preview Request"),
            createTopic("server-info", "Server Information", "server", "server-information"),
            createTopic("commands-options", "Commands and Options", "commands", "options"),
            createTopic("supported-formats", "Supported Formats", "formats"),
            createTopic("extraction-rules", "Extraction Rules", "extraction"),
            createTopic("clustering-settings", "Clustering Settings", "clustering"),
            createTopic("output-columns", "Output Columns", "output", "output-format"),
            createTopic("exit-codes", "Exit Codes", "exit"),
            createTopic("privacy", "Privacy"),
            createTopic("troubleshooting", "Troubleshooting", "problems")
        };

        var topicsByKey = new Dictionary<string, HelpTopic>(StringComparer.OrdinalIgnoreCase);
        foreach (var topic in Topics)
        {
            addLookupKey(topicsByKey, topic.Key, topic);
            foreach (var alias in topic.Aliases)
            {
                addLookupKey(topicsByKey, alias, topic);
            }
        }

        _topicsByKey = topicsByKey;

        #endregion
    }

    /**************************************************************/
    /// <summary>Gets every help topic in interactive display order.</summary>
    public IReadOnlyList<HelpTopic> Topics { get; }

    /**************************************************************/
    /// <summary>Gets the topic displayed when no explicit topic is supplied.</summary>
    public HelpTopic DefaultTopic => Topics[0];

    /**************************************************************/
    /// <summary>
    /// Resolves an optional topic key after applying case, whitespace, and underscore normalization.
    /// </summary>
    /// <param name="requestedTopic">The optional canonical key or alias supplied by a user.</param>
    /// <param name="topic">The resolved topic, or <see langword="null"/> when resolution fails.</param>
    /// <returns><see langword="true"/> when the requested topic exists; otherwise <see langword="false"/>.</returns>
    internal bool TryResolve(
        string? requestedTopic,
        [NotNullWhen(true)] out HelpTopic? topic)
    {
        #region implementation

        if (string.IsNullOrWhiteSpace(requestedTopic))
        {
            topic = DefaultTopic;
            return true;
        }

        return _topicsByKey.TryGetValue(normalizeKey(requestedTopic), out topic);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Creates one topic with the resource naming convention used by the project file.
    /// </summary>
    /// <param name="key">The topic's canonical key and Markdown filename stem.</param>
    /// <param name="title">The human-readable menu label.</param>
    /// <param name="aliases">Optional alternative lookup names.</param>
    /// <returns>The immutable topic definition.</returns>
    private static HelpTopic createTopic(string key, string title, params string[] aliases)
    {
        #region implementation

        return new HelpTopic
        {
            Key = key,
            Title = title,
            ResourceName = $"{ResourcePrefix}{key}.md",
            Aliases = aliases
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Adds one normalized lookup key while failing fast on ambiguous catalog definitions.
    /// </summary>
    /// <param name="lookup">The mutable lookup under construction.</param>
    /// <param name="key">The canonical key or alias to add.</param>
    /// <param name="topic">The topic represented by the key.</param>
    /// <exception cref="InvalidOperationException">Thrown when the normalized key is already present.</exception>
    private static void addLookupKey(IDictionary<string, HelpTopic> lookup, string key, HelpTopic topic)
    {
        #region implementation

        var normalizedKey = normalizeKey(key);
        if (!lookup.TryAdd(normalizedKey, topic))
        {
            throw new InvalidOperationException($"Duplicate help topic key or alias: {normalizedKey}");
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Normalizes a human-entered topic name to the catalog's hyphenated key convention.
    /// </summary>
    /// <param name="key">The raw topic key or alias.</param>
    /// <returns>The trimmed, lowercase, hyphenated lookup key.</returns>
    private static string normalizeKey(string key)
    {
        #region implementation

        return string.Join(
            '-',
            key.Trim()
                .Replace('_', '-')
                .Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();

        #endregion
    }

    #endregion
}
