using System.Text.Json;
using Carrot.Cli.CarrotApi.Contracts;
using Carrot.Cli.Cli.UI;
using Spectre.Console.Testing;
using Xunit;

namespace Carrot.Cli.Tests.Cli;

/**************************************************************/
/// <summary>
/// Verifies terminal-sized paging and Escape-compatible navigation for Server Information output.
/// </summary>
public sealed class ServerInformationPagerTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Verifies a large response pages forward and returns through the Back action.</summary>
    [Fact]
    public async Task ShowAsync_LargeResponse_PagesAndReturnsOnBack()
    {
        #region implementation

        // Arrange
        using var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Profile.Height = 12;
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        var pager = new ServerInformationPager(console, new ConsoleReporter(console));
        var configuration = new ListResponse
        {
            Algorithms = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["Algorithm A"] = ["English"],
                ["Algorithm B"] = ["English"],
                ["Algorithm C"] = ["English"],
                ["Algorithm D"] = ["English"]
            },
            Templates = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["template"] = JsonSerializer.SerializeToElement(new { algorithm = "Algorithm A" })
            }
        };

        // Act
        await pager.ShowAsync(configuration, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("Page 1/", console.Output, StringComparison.Ordinal);
        Assert.Contains("Page 2/", console.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Page 3/", console.Output, StringComparison.Ordinal);
        Assert.Contains("Back to Server Information", console.Output, StringComparison.Ordinal);

        #endregion
    }

    #endregion
}
