using Carrot.Cli.Cli.UI;
using Spectre.Console.Testing;
using Xunit;

namespace Carrot.Cli.Tests.Cli;

/**************************************************************/
/// <summary>
/// Verifies topic resolution, embedded resources, safe Markdown presentation, and About output.
/// </summary>
public sealed class HelpSystemTests
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Verifies every catalog topic resolves by key and has nonempty embedded Markdown content.
    /// </summary>
    [Fact]
    public void Catalog_AllTopicsResolveToEmbeddedMarkdown()
    {
        #region implementation

        // Arrange
        var catalog = new HelpTopicCatalog();
        var provider = new EmbeddedHelpContentProvider();

        // Act and Assert
        Assert.Equal(13, catalog.Topics.Count);
        foreach (var expectedTopic in catalog.Topics)
        {
            Assert.True(catalog.TryResolve(expectedTopic.Key, out var resolvedTopic));
            Assert.Same(expectedTopic, resolvedTopic);
            Assert.False(string.IsNullOrWhiteSpace(provider.Read(expectedTopic)));
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies help aliases normalize spaces, underscores, hyphens, and case consistently.
    /// </summary>
    /// <param name="input">The user-entered topic key or alias.</param>
    /// <param name="expectedKey">The expected canonical key.</param>
    [Theory]
    [InlineData(null, "getting-started")]
    [InlineData("SERVER_INFORMATION", "server-info")]
    [InlineData("Output Format", "output-columns")]
    [InlineData("SCHEDULING", "task-scheduler")]
    public void Catalog_KnownAlias_ResolvesCanonicalTopic(string? input, string expectedKey)
    {
        #region implementation

        // Arrange
        var catalog = new HelpTopicCatalog();

        // Act
        var resolved = catalog.TryResolve(input, out var topic);

        // Assert
        Assert.True(resolved);
        Assert.NotNull(topic);
        Assert.Equal(expectedKey, topic.Key);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies unknown help reports all valid keys without throwing.
    /// </summary>
    [Fact]
    public void Render_UnknownTopic_ReturnsFalseAndListsTopics()
    {
        #region implementation

        // Arrange
        using var console = new TestConsole();
        var renderer = createHelpRenderer(console);

        // Act
        var rendered = renderer.Render("not-a-topic");

        // Assert
        Assert.False(rendered);
        Assert.Contains("Unknown help topic", console.Output, StringComparison.Ordinal);
        Assert.Contains("getting-started", console.Output, StringComparison.Ordinal);
        Assert.Contains("troubleshooting", console.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies missing embedded content produces a controlled failure message.
    /// </summary>
    [Fact]
    public void ContentProvider_UnknownResource_ReturnsNull()
    {
        #region implementation

        // Arrange
        var provider = new EmbeddedHelpContentProvider();
        var topic = new HelpTopic
        {
            Key = "missing",
            Title = "Missing",
            ResourceName = "Carrot.Cli.Help.missing.md"
        };

        // Act
        var content = provider.Read(topic);

        // Assert
        Assert.Null(content);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies supported Markdown renders legibly while Spectre-like source text stays literal.
    /// </summary>
    [Fact]
    public void Render_MarkdownSubset_FormatsAndEscapesContent()
    {
        #region implementation

        // Arrange
        using var console = new TestConsole();
        var renderer = new MarkdownHelpRenderer(console);
        const string markdown = """
            # Heading

            Text with **bold**, *italic*, `code`, a [link](https://example.test), and [red]literal[/].

            - Bullet
            1. Step

            ```text
            [blue]literal code[/]
            ```
            """;

        // Act
        renderer.Render(markdown);

        // Assert
        Assert.Contains("Heading", console.Output, StringComparison.Ordinal);
        Assert.Contains("bold", console.Output, StringComparison.Ordinal);
        Assert.Contains("code", console.Output, StringComparison.Ordinal);
        Assert.Contains("https://example.test", console.Output, StringComparison.Ordinal);
        Assert.Contains("• Bullet", console.Output, StringComparison.Ordinal);
        Assert.Contains("1. Step", console.Output, StringComparison.Ordinal);
        Assert.Contains("[red]literal[/]", console.Output, StringComparison.Ordinal);
        Assert.Contains("[blue]literal code[/]", console.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies About includes version, compatibility, formats, and notice information.
    /// </summary>
    [Fact]
    public void Render_About_DisplaysApplicationMetadata()
    {
        #region implementation

        // Arrange
        using var console = new TestConsole();
        var renderer = new AboutRenderer(console);

        // Act
        renderer.Render();

        // Assert
        Assert.Contains("Carrot CLI", console.Output, StringComparison.Ordinal);
        Assert.Contains("Carrot 4.8.6", console.Output, StringComparison.Ordinal);
        Assert.Contains(".docx", console.Output, StringComparison.Ordinal);
        Assert.Contains("THIRD-PARTY-NOTICES.md", console.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Creates the real help pipeline against a test console.
    /// </summary>
    /// <param name="console">The console receiving rendered output.</param>
    /// <returns>The fully wired help renderer.</returns>
    private static HelpRenderer createHelpRenderer(TestConsole console)
    {
        #region implementation

        return new HelpRenderer(
            console,
            new HelpTopicCatalog(),
            new EmbeddedHelpContentProvider(),
            new MarkdownHelpRenderer(console));

        #endregion
    }

    #endregion
}
