using System.Text;
using System.Text.Json;
using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace Carrot.Cli.Tests.Configuration;

/**************************************************************/
/// <summary>Verifies clustering defaults, template precedence, and bounded parameter-file parsing.</summary>
public sealed class ClusteringConfigurationResolverTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Verifies an empty selection preserves the configured direct-selection defaults.</summary>
    [Fact]
    public async Task ResolveAsync_EmptySelection_ReturnsConfiguredDefaults()
    {
        #region implementation

        // Arrange
        var resolver = createResolver(defaultAlgorithm: "STC", defaultLanguage: "French");

        // Act
        var result = await resolver.ResolveAsync(
            new ClusteringSelection(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal("STC", result.Value!.Algorithm);
        Assert.Equal("French", result.Value.Language);
        Assert.Null(result.Value.Template);
        Assert.Null(result.Value.Parameters);

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies explicit direct identifiers are trimmed without changing their case.</summary>
    [Fact]
    public async Task ResolveAsync_ExplicitDirectSelection_PreservesExactIdentifiers()
    {
        #region implementation

        // Arrange
        var resolver = createResolver();

        // Act
        var result = await resolver.ResolveAsync(
            new ClusteringSelection { Algorithm = "  BisectingKMeans  ", Language = "  Polish  " },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal("BisectingKMeans", result.Value!.Algorithm);
        Assert.Equal("Polish", result.Value.Language);

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies template selection omits body algorithm and language fields.</summary>
    [Fact]
    public async Task ResolveAsync_TemplateSelection_OmitsAlgorithmAndLanguage()
    {
        #region implementation

        // Arrange
        var resolver = createResolver();

        // Act
        var result = await resolver.ResolveAsync(
            new ClusteringSelection { Template = "  news  " },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Null(result.Value!.Algorithm);
        Assert.Null(result.Value.Language);
        Assert.Equal("news", result.Value.Template);

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies a template cannot be combined with either explicit direct-selection field.</summary>
    /// <param name="algorithm">The optional explicit algorithm.</param>
    /// <param name="language">The optional explicit language.</param>
    [Theory]
    [InlineData("Lingo", null)]
    [InlineData(null, "English")]
    [InlineData("Lingo", "English")]
    public async Task ResolveAsync_TemplateAndDirectSelection_ReturnsAmbiguousFailure(
        string? algorithm,
        string? language)
    {
        #region implementation

        // Arrange
        var resolver = createResolver();

        // Act
        var result = await resolver.ResolveAsync(
            new ClusteringSelection { Template = "news", Algorithm = algorithm, Language = language },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OperationStatus.Failure, result.Status);
        Assert.Equal("clustering.selection.ambiguous", Assert.Single(result.Messages).Code);

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies explicitly empty identifiers produce field-specific structured failures.</summary>
    /// <param name="field">The selection field receiving whitespace.</param>
    /// <param name="expectedCode">The stable expected failure code.</param>
    [Theory]
    [InlineData("algorithm", "clustering.algorithm.empty")]
    [InlineData("language", "clustering.language.empty")]
    [InlineData("template", "clustering.template.empty")]
    public async Task ResolveAsync_WhitespaceIdentifier_ReturnsStructuredFailure(
        string field,
        string expectedCode)
    {
        #region implementation

        // Arrange
        var resolver = createResolver();
        var selection = field switch
        {
            "algorithm" => new ClusteringSelection { Algorithm = "  " },
            "language" => new ClusteringSelection { Language = "  " },
            "template" => new ClusteringSelection { Template = "  " },
            _ => throw new InvalidOperationException($"Unsupported test field: {field}")
        };

        // Act
        var result = await resolver.ResolveAsync(selection, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OperationStatus.Failure, result.Status);
        Assert.Equal(expectedCode, Assert.Single(result.Messages).Code);

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies every JSON value kind survives parsing after the source document is disposed.</summary>
    [Fact]
    public async Task ResolveAsync_NestedParameterObject_PreservesDetachedJsonValues()
    {
        #region implementation

        // Arrange
        var root = createTemporaryDirectory();
        var path = Path.Combine(root, "parameters.json");
        const string json = """
            {
              "number": 2.5,
              "enabled": true,
              "formula": "=SUM(A1:A2)",
              "items": [1, "two", null],
              "nested": { "label": "Ω" },
              "nothing": null
            }
            """;
        File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        try
        {
            var resolver = createResolver();

            // Act
            var result = await resolver.ResolveAsync(
                new ClusteringSelection { ParametersFile = path },
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(OperationStatus.Success, result.Status);
            var parameters = Assert.IsAssignableFrom<IReadOnlyDictionary<string, JsonElement>>(
                result.Value!.Parameters);
            Assert.Equal(JsonValueKind.Number, parameters["number"].ValueKind);
            Assert.Equal(2.5D, parameters["number"].GetDouble());
            Assert.True(parameters["enabled"].GetBoolean());
            Assert.Equal("=SUM(A1:A2)", parameters["formula"].GetString());
            Assert.Equal(JsonValueKind.Array, parameters["items"].ValueKind);
            Assert.Equal("Ω", parameters["nested"].GetProperty("label").GetString());
            Assert.Equal(JsonValueKind.Null, parameters["nothing"].ValueKind);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies invalid JSON shapes and duplicate properties fail before configuration use.</summary>
    /// <param name="json">The invalid parameter-file content.</param>
    /// <param name="expectedCode">The stable expected failure code.</param>
    [Theory]
    [InlineData("[]", "clustering.parameters.root")]
    [InlineData("null", "clustering.parameters.root")]
    [InlineData("{ not-json }", "clustering.parameters.invalid-json")]
    [InlineData("{ \"value\": 1, \"value\": 2 }", "clustering.parameters.duplicate")]
    public async Task ResolveAsync_InvalidParameterContent_ReturnsStructuredFailure(
        string json,
        string expectedCode)
    {
        #region implementation

        // Arrange
        var root = createTemporaryDirectory();
        var path = Path.Combine(root, "parameters.json");
        File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        try
        {
            var resolver = createResolver();

            // Act
            var result = await resolver.ResolveAsync(
                new ClusteringSelection { ParametersFile = path },
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(OperationStatus.Failure, result.Status);
            Assert.Equal(expectedCode, Assert.Single(result.Messages).Code);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies malformed UTF-8 is rejected as invalid JSON rather than replacement text.</summary>
    [Fact]
    public async Task ResolveAsync_InvalidUtf8_ReturnsInvalidJsonFailure()
    {
        #region implementation

        // Arrange
        var root = createTemporaryDirectory();
        var path = Path.Combine(root, "parameters.json");
        File.WriteAllBytes(path, [0x7B, 0x22, 0x78, 0x22, 0x3A, 0x22, 0xC3, 0x28, 0x22, 0x7D]);

        try
        {
            var resolver = createResolver();

            // Act
            var result = await resolver.ResolveAsync(
                new ClusteringSelection { ParametersFile = path },
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(OperationStatus.Failure, result.Status);
            Assert.Equal("clustering.parameters.invalid-json", Assert.Single(result.Messages).Code);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies the byte limit accepts its boundary and rejects the next lower configured limit.</summary>
    /// <param name="maximumBytes">The configured maximum for the seven-byte fixture.</param>
    /// <param name="expectedSuccess">Whether resolution should succeed.</param>
    [Theory]
    [InlineData(7, true)]
    [InlineData(6, false)]
    public async Task ResolveAsync_ParameterFileSizeBoundary_EnforcesByteLimit(
        int maximumBytes,
        bool expectedSuccess)
    {
        #region implementation

        // Arrange
        var root = createTemporaryDirectory();
        var path = Path.Combine(root, "parameters.json");
        File.WriteAllBytes(path, "{\"a\":1}"u8.ToArray());

        try
        {
            var resolver = createResolver(maximumParameterFileBytes: maximumBytes);

            // Act
            var result = await resolver.ResolveAsync(
                new ClusteringSelection { ParametersFile = path },
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(expectedSuccess, result.Status == OperationStatus.Success);
            if (!expectedSuccess)
            {
                Assert.Equal("clustering.parameters.too-large", Assert.Single(result.Messages).Code);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies missing and incorrectly named parameter files return expected failures.</summary>
    /// <param name="fileName">The nonexistent selected filename.</param>
    /// <param name="expectedCode">The stable expected failure code.</param>
    [Theory]
    [InlineData("missing.json", "clustering.parameters.missing")]
    [InlineData("parameters.txt", "clustering.parameters.extension")]
    public async Task ResolveAsync_InvalidParameterPath_ReturnsStructuredFailure(
        string fileName,
        string expectedCode)
    {
        #region implementation

        // Arrange
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), fileName);
        var resolver = createResolver();

        // Act
        var result = await resolver.ResolveAsync(
            new ClusteringSelection { ParametersFile = path },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OperationStatus.Failure, result.Status);
        Assert.Equal(expectedCode, Assert.Single(result.Messages).Code);

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies a file locked against sharing returns an unreadable-file failure.</summary>
    [Fact]
    public async Task ResolveAsync_ExclusivelyLockedParameterFile_ReturnsUnreadableFailure()
    {
        #region implementation

        // Arrange
        var root = createTemporaryDirectory();
        var path = Path.Combine(root, "parameters.json");
        File.WriteAllText(path, "{}");
        try
        {
            using var exclusiveLock = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            var resolver = createResolver();

            // Act
            var result = await resolver.ResolveAsync(
                new ClusteringSelection { ParametersFile = path },
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(OperationStatus.Failure, result.Status);
            Assert.Equal("clustering.parameters.unreadable", Assert.Single(result.Messages).Code);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies caller cancellation propagates instead of becoming a validation failure.</summary>
    [Fact]
    public async Task ResolveAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        #region implementation

        // Arrange
        var resolver = createResolver();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        // Act and assert
#pragma warning disable xUnit1051 // The test intentionally supplies an already-canceled controlled token.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => resolver.ResolveAsync(new ClusteringSelection(), cancellationSource.Token));
#pragma warning restore xUnit1051

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies constructor and public resolution guards reject null dependencies and inputs.</summary>
    [Fact]
    public async Task ConstructorAndResolveAsync_NullValues_ThrowArgumentNullException()
    {
        #region implementation

        // Act and assert
        Assert.Throws<ArgumentNullException>(() => new ClusteringConfigurationResolver(null!));
        var resolver = createResolver();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => resolver.ResolveAsync(null!, TestContext.Current.CancellationToken));

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a resolver with deterministic defaults and a configurable file-size limit.</summary>
    /// <param name="defaultAlgorithm">The direct-selection algorithm default.</param>
    /// <param name="defaultLanguage">The direct-selection language default.</param>
    /// <param name="maximumParameterFileBytes">The parameter-file byte limit.</param>
    /// <returns>The configured resolver.</returns>
    private static ClusteringConfigurationResolver createResolver(
        string defaultAlgorithm = "Lingo",
        string defaultLanguage = "English",
        int maximumParameterFileBytes = 1024 * 1024)
    {
        #region implementation

        return new ClusteringConfigurationResolver(Options.Create(new CarrotCliOptions
        {
            DefaultAlgorithm = defaultAlgorithm,
            DefaultLanguage = defaultLanguage,
            MaximumParameterFileBytes = maximumParameterFileBytes
        }));

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one isolated test directory.</summary>
    /// <returns>The absolute directory path.</returns>
    private static string createTemporaryDirectory()
    {
        #region implementation

        var path = Path.Combine(Path.GetTempPath(), "CarrotCliTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;

        #endregion
    }

    #endregion
}
