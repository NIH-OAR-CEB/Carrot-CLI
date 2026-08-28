using Carrot.Cli.Cli.UI;
using Spectre.Console.Testing;
using Xunit;

namespace Carrot.Cli.Tests.Cli;

/**************************************************************/
/// <summary>
/// Verifies the external application preamble file boundary and its resilient presentation.
/// </summary>
public sealed class ApplicationPreambleTests
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Verifies an existing provider observes file edits instead of retaining compiled or cached content.
    /// </summary>
    [Fact]
    public void Read_UpdatedFile_ReturnsLatestContentWithoutRebuild()
    {
        #region implementation

        // Arrange
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"CarrotCliPreambleTests-{Guid.NewGuid():N}");
        var preamblePath = Path.Combine(temporaryDirectory, "application-preamble.md");
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            File.WriteAllText(preamblePath, "# Original Welcome");
            var provider = new FileApplicationPreambleProvider(preamblePath);

            // Act and assert
            Assert.Equal("# Original Welcome", provider.Read());
            File.WriteAllText(preamblePath, "# Updated Welcome");
            Assert.Equal("# Updated Welcome", provider.Read());
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies a missing or blank preamble does not prevent operators from reaching the menu.
    /// </summary>
    [Fact]
    public void Render_MissingContent_DisplaysFallbackGuidance()
    {
        #region implementation

        // Arrange
        using var console = new TestConsole();
        var renderer = new ApplicationPreambleRenderer(
            console,
            new MissingApplicationPreambleProvider(),
            new MarkdownHelpRenderer(console));

        // Act
        renderer.Render();

        // Assert
        Assert.Contains("Welcome information is unavailable", console.Output, StringComparison.Ordinal);
        Assert.Contains("help getting-started", console.Output, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>Represents an unavailable external preamble file.</summary>
    private sealed class MissingApplicationPreambleProvider : IApplicationPreambleProvider
    {
        #region implementation

        /**************************************************************/
        /// <summary>Returns no content to exercise the recovery presentation.</summary>
        /// <returns><see langword="null"/>.</returns>
        public string? Read()
        {
            #region implementation

            return null;

            #endregion
        }

        #endregion
    }

    #endregion
}
