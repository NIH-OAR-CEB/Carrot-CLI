using Carrot.Cli.Configuration;
using Xunit;

namespace Carrot.Cli.Tests.Configuration;

/**************************************************************/
/// <summary>Verifies workbook preview configuration remains inside Excel's cell limit.</summary>
public sealed class CarrotCliOptionsValidatorTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Verifies the public validator accepts the exact Excel limit and rejects the next value.</summary>
    /// <param name="limit">The configured workbook preview limit.</param>
    /// <param name="expectedSuccess">Whether validation should succeed.</param>
    [Theory]
    [InlineData(32_767, true)]
    [InlineData(32_768, false)]
    public void Validate_ContentPreviewBoundary_EnforcesExcelCellLimit(int limit, bool expectedSuccess)
    {
        #region implementation

        // Arrange
        var validator = new CarrotCliOptionsValidator();
        var options = new CarrotCliOptions { ContentPreviewCharacterLimit = limit };

        // Act
        var result = validator.Validate(name: null, options);

        // Assert
        Assert.Equal(expectedSuccess, result.Succeeded);
        if (!expectedSuccess)
        {
            Assert.NotNull(result.Failures);
            Assert.Contains(result.Failures, failure => failure.Contains("32,767", StringComparison.Ordinal));
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies the parameter-file byte limit must remain positive.</summary>
    /// <param name="limit">The configured parameter-file limit.</param>
    /// <param name="expectedSuccess">Whether validation should succeed.</param>
    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    public void Validate_ParameterFileLimit_RequiresPositiveValue(int limit, bool expectedSuccess)
    {
        #region implementation

        // Arrange
        var validator = new CarrotCliOptionsValidator();
        var options = new CarrotCliOptions { MaximumParameterFileBytes = limit };

        // Act
        var result = validator.Validate(name: null, options);

        // Assert
        Assert.Equal(expectedSuccess, result.Succeeded);
        if (!expectedSuccess)
        {
            Assert.Contains(
                result.Failures!,
                failure => failure.Contains(nameof(CarrotCliOptions.MaximumParameterFileBytes), StringComparison.Ordinal));
        }

        #endregion
    }

    #endregion
}
