using Carrot.Cli.Processing;
using Xunit;

namespace Carrot.Cli.Tests.Processing;

/**************************************************************/
/// <summary>Verifies production run identifier generation used by successful processed batches.</summary>
public sealed class SystemRunIdProviderTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Verifies each public creation call returns a nonempty independent identifier.</summary>
    [Fact]
    public void Create_TwoCalls_ReturnsDistinctNonemptyIdentifiers()
    {
        #region implementation

        // Arrange
        var provider = new SystemRunIdProvider();

        // Act
        var first = provider.Create();
        var second = provider.Create();

        // Assert
        Assert.NotEqual(Guid.Empty, first);
        Assert.NotEqual(Guid.Empty, second);
        Assert.NotEqual(first, second);

        #endregion
    }

    #endregion
}
