using Carrot.Cli.Cli.Settings;
using Spectre.Console;
using Xunit;

namespace Carrot.Cli.Tests.ISearch;

/**************************************************************/
/// <summary>Verifies named iSearch advanced option validation and defaults.</summary>
/// <seealso cref="ISearchSettings"/>
public sealed class ISearchSettingsTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Ensures omitted advanced values use the interactive builder's safe defaults.</summary>
    [Fact]
    public void Validate_Defaults_AreMatchAllAndBounded()
    {
        #region implementation

        var settings = new ISearchSettings
        {
            Database = "grants",
            ResultDataset = "Grants"
        };

        var result = settings.Validate();

        Assert.Null(result.Message);
        Assert.Equal("*:*", settings.Query);
        Assert.Equal("AND", settings.DefaultOp);
        Assert.Equal(100, settings.Rows);
        Assert.Null(settings.MaxResults);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures advanced option relationships reject conflicting or malformed values.</summary>
    [Theory]
    [InlineData("--all-results cannot be combined with --max-results.")]
    [InlineData("--updated-after cannot be later than --updated-before.")]
    [InlineData("--categorized-output requires --categorize.")]
    public void Validate_InvalidRelationships_ReturnErrors(string expected)
    {
        #region implementation

        var settings = new ISearchSettings
        {
            Database = "grants",
            ResultDataset = "Grants",
            AllResults = expected.StartsWith("--all-results", StringComparison.Ordinal),
            MaxResults = expected.StartsWith("--all-results", StringComparison.Ordinal) ? 5 : null,
            UpdatedAfter = expected.StartsWith("--updated-after", StringComparison.Ordinal) ? "2025-01-01" : null,
            UpdatedBefore = expected.StartsWith("--updated-after", StringComparison.Ordinal) ? "2024-01-01" : null,
            CategorizedOutputPath = expected.StartsWith("--categorized-output", StringComparison.Ordinal) ? "results.xlsx" : null
        };

        var result = settings.Validate();

        Assert.NotEqual(ValidationResult.Success(), result);
        Assert.Contains(expected, result.Message, StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures every advanced scalar stays within its service-safe boundary.</summary>
    [Theory]
    [InlineData(0, "--rows must be between 1 and 100.")]
    [InlineData(101, "--rows must be between 1 and 100.")]
    public void Validate_InvalidRows_ReturnsError(int rows, string expected)
    {
        #region implementation

        var settings = new ISearchSettings
        {
            Database = "grants",
            ResultDataset = "Grants",
            Rows = rows
        };

        var result = settings.Validate();

        Assert.Contains(expected, result.Message, StringComparison.Ordinal);

        #endregion
    }

    #endregion
}
