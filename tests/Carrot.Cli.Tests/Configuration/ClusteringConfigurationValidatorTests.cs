using System.Text.Json;
using Carrot.Cli.CarrotApi.Contracts;
using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Xunit;

namespace Carrot.Cli.Tests.Configuration;

/**************************************************************/
/// <summary>Verifies exact direct and template clustering selections against Carrot list metadata.</summary>
public sealed class ClusteringConfigurationValidatorTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Verifies an exact compatible algorithm/language pair returns the same configuration.</summary>
    [Fact]
    public void Validate_ExactDirectSelection_ReturnsSuccess()
    {
        #region implementation

        // Arrange
        var validator = new ClusteringConfigurationValidator();
        var configuration = new ClusteringConfiguration { Algorithm = "Lingo", Language = "English" };

        // Act
        var result = validator.Validate(configuration, createListResponse());

        // Assert
        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Same(configuration, result.Value);

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies algorithm and language matching remains ordinal even for insensitive dictionaries.</summary>
    /// <param name="algorithm">The requested algorithm.</param>
    /// <param name="language">The requested language.</param>
    /// <param name="expectedCode">The stable expected failure code.</param>
    [Theory]
    [InlineData("lingo", "English", "clustering.algorithm.unavailable")]
    [InlineData("Lingo", "english", "clustering.language.unavailable")]
    [InlineData("STC", "English", "clustering.language.unavailable")]
    public void Validate_InexactOrIncompatibleDirectSelection_ReturnsFailure(
        string algorithm,
        string language,
        string expectedCode)
    {
        #region implementation

        // Arrange
        var validator = new ClusteringConfigurationValidator();
        var listResponse = new ListResponse
        {
            Algorithms = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Lingo"] = ["English"],
                ["STC"] = ["Polish"]
            },
            Templates = new Dictionary<string, JsonElement>()
        };

        // Act
        var result = validator.Validate(
            new ClusteringConfiguration { Algorithm = algorithm, Language = language },
            listResponse);

        // Assert
        Assert.Equal(OperationStatus.Failure, result.Status);
        Assert.Equal(expectedCode, Assert.Single(result.Messages).Code);

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies template identifiers are validated exactly and do not require body selections.</summary>
    /// <param name="template">The requested template.</param>
    /// <param name="expectedSuccess">Whether exact validation should succeed.</param>
    [Theory]
    [InlineData("news", true)]
    [InlineData("News", false)]
    public void Validate_TemplateSelection_UsesExactAdvertisedIdentifier(
        string template,
        bool expectedSuccess)
    {
        #region implementation

        // Arrange
        var validator = new ClusteringConfigurationValidator();

        // Act
        var result = validator.Validate(
            new ClusteringConfiguration { Template = template },
            createListResponse());

        // Assert
        Assert.Equal(expectedSuccess, result.Status == OperationStatus.Success);
        if (!expectedSuccess)
        {
            Assert.Equal("clustering.template.unavailable", Assert.Single(result.Messages).Code);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies malformed resolved configurations are rejected defensively.</summary>
    /// <param name="template">The optional template.</param>
    /// <param name="algorithm">The optional algorithm.</param>
    /// <param name="language">The optional language.</param>
    /// <param name="expectedCode">The stable expected failure code.</param>
    [Theory]
    [InlineData("news", "Lingo", null, "clustering.selection.ambiguous")]
    [InlineData(null, "Lingo", null, "clustering.selection.incomplete")]
    [InlineData(null, null, "English", "clustering.selection.incomplete")]
    public void Validate_InvalidResolvedShape_ReturnsFailure(
        string? template,
        string? algorithm,
        string? language,
        string expectedCode)
    {
        #region implementation

        // Arrange
        var validator = new ClusteringConfigurationValidator();

        // Act
        var result = validator.Validate(
            new ClusteringConfiguration { Template = template, Algorithm = algorithm, Language = language },
            createListResponse());

        // Assert
        Assert.Equal(OperationStatus.Failure, result.Status);
        Assert.Equal(expectedCode, Assert.Single(result.Messages).Code);

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies the public validation method guards both required arguments.</summary>
    [Fact]
    public void Validate_NullArguments_ThrowArgumentNullException()
    {
        #region implementation

        // Arrange
        var validator = new ClusteringConfigurationValidator();
        var configuration = new ClusteringConfiguration { Algorithm = "Lingo", Language = "English" };
        var listResponse = createListResponse();

        // Act and assert
        Assert.Throws<ArgumentNullException>(() => validator.Validate(null!, listResponse));
        Assert.Throws<ArgumentNullException>(() => validator.Validate(configuration, null!));

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates deterministic algorithm, language, and template metadata.</summary>
    /// <returns>The list-response fixture.</returns>
    private static ListResponse createListResponse()
    {
        #region implementation

        return new ListResponse
        {
            Algorithms = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["Lingo"] = ["English"],
                ["STC"] = ["Polish"]
            },
            Templates = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["news"] = JsonSerializer.SerializeToElement(new { algorithm = "Lingo" })
            }
        };

        #endregion
    }

    #endregion
}
