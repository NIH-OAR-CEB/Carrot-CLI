using Carrot.Cli.Reporting;
using Xunit;

namespace Carrot.Cli.Tests.Reporting;

/**************************************************************/
/// <summary>Verifies readable Excel output suggestions and their existing-directory fallback.</summary>
public sealed class ExcelOutputPathSuggesterTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Verifies the suggestion is absolute, valid, timestamped, and rooted under an existing directory.</summary>
    [Fact]
    public void Suggest_FixedLocalTime_ReturnsCompleteTimestampedWorkbookPath()
    {
        #region implementation

        // Arrange
        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(2026, 8, 31, 10, 15, 42, TimeSpan.FromHours(-4)),
            TimeZoneInfo.CreateCustomTimeZone("Test Eastern", TimeSpan.FromHours(-4), "Test Eastern", "Test Eastern"));
        var suggester = new ExcelOutputPathSuggester(timeProvider);

        // Act
        var result = suggester.Suggest();

        // Assert
        Assert.True(Path.IsPathFullyQualified(result));
        Assert.Equal("carrot-results-20260831-101542.xlsx", Path.GetFileName(result));
        Assert.True(Directory.Exists(Path.GetDirectoryName(result)));

        #endregion
    }

    /**************************************************************/
    /// <summary>Supplies deterministic time and local-zone values to the production suggester.</summary>
    private sealed class FixedTimeProvider : TimeProvider
    {
        #region implementation

        private readonly DateTimeOffset _utcNow;
        private readonly TimeZoneInfo _localTimeZone;

        /**************************************************************/
        /// <summary>Initializes the provider with fixed clock and time-zone values.</summary>
        /// <param name="localNow">The desired local time and offset.</param>
        /// <param name="localTimeZone">The zone used by <see cref="TimeProvider.GetLocalNow"/>.</param>
        internal FixedTimeProvider(DateTimeOffset localNow, TimeZoneInfo localTimeZone)
        {
            #region implementation

            _utcNow = localNow.ToUniversalTime();
            _localTimeZone = localTimeZone;

            #endregion
        }

        /**************************************************************/
        /// <summary>Gets the deterministic local time zone.</summary>
        public override TimeZoneInfo LocalTimeZone => _localTimeZone;

        /**************************************************************/
        /// <summary>Returns the fixed UTC instant.</summary>
        /// <returns>The configured instant.</returns>
        public override DateTimeOffset GetUtcNow()
        {
            #region implementation

            return _utcNow;

            #endregion
        }

        #endregion
    }

    #endregion
}
