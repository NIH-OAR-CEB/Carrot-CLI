using Carrot.Cli.Cli.Settings;
using Carrot.Cli.Common;
using Carrot.Cli.Composition;
using Carrot.Cli.Configuration;
using Carrot.Cli.Processing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Carrot.Cli.Tests.Configuration;

/**************************************************************/
/// <summary>
/// Verifies named-command endpoint precedence, defaults, path normalization, and safe validation failures.
/// </summary>
public sealed class RunSettingsResolverTests
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Verifies every process option is normalized into the immutable request and an explicit endpoint wins.
    /// </summary>
    [Fact]
    public void ResolveProcess_AllSettings_ReturnsNormalizedRequest()
    {
        #region implementation

        // Arrange
        var root = createTemporaryDirectory();
        try
        {
            var input = Directory.CreateDirectory(Path.Combine(root, "input documents")).FullName;
            var parametersFile = Path.Combine(root, "parameters.json");
            File.WriteAllText(parametersFile, "{}");
            var outputPath = Path.Combine(root, "report.xlsx");
            var logFile = Path.Combine(root, "run.log");
            var configuration = createConfiguration(
                new Dictionary<string, string?>
                {
                    ["CARROTCLI_ENDPOINT"] = "https://environment.example/service"
                });
            using var provider = createProvider(configuration);
            var resolver = provider.GetRequiredService<RunSettingsResolver>();
            var settings = new ProcessSettings
            {
                InputPath = $"\"{input}\"",
                Endpoint = " https://explicit.example/service/ ",
                OutputPath = $"'{outputPath}'",
                Recursive = true,
                Algorithm = "  STC  ",
                Language = "  English  ",
                ParametersFile = $"\"{parametersFile}\"",
                TimeoutSeconds = 45,
                Overwrite = true,
                NoJsonArtifacts = true,
                Quiet = true,
                LogFile = logFile
            };

            // Act
            var result = resolver.ResolveProcess(settings);

            // Assert
            Assert.Equal(OperationStatus.Success, result.Status);
            Assert.Empty(result.Messages);
            var request = Assert.IsType<ProcessRequest>(result.Value);
            Assert.Equal(input, request.InputPath);
            Assert.Equal("https://explicit.example/service", request.Endpoint.AbsoluteUri.TrimEnd('/'));
            Assert.Equal(outputPath, request.OutputPath);
            Assert.True(request.Recursive);
            Assert.Equal("STC", request.Clustering.Algorithm);
            Assert.Equal("English", request.Clustering.Language);
            Assert.Null(request.Clustering.Template);
            Assert.Equal(parametersFile, request.Clustering.ParametersFile);
            Assert.Equal(TimeSpan.FromSeconds(45), request.Timeout);
            Assert.True(request.Overwrite);
            Assert.False(request.WriteJsonArtifacts);
            Assert.True(request.Quiet);
            Assert.Equal(logFile, request.LogFile);
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies a template selection suppresses configured direct-selection defaults.</summary>
    [Fact]
    public void ResolvePreview_TemplateSelection_OmitsAlgorithmAndLanguage()
    {
        #region implementation

        // Arrange
        var root = createTemporaryDirectory();
        try
        {
            var input = Directory.CreateDirectory(Path.Combine(root, "input")).FullName;
            using var provider = createProvider(
                createConfiguration(
                    new Dictionary<string, string?>
                    {
                        ["CARROTCLI_ENDPOINT"] = "http://localhost:8080/service"
                    }));
            var resolver = provider.GetRequiredService<RunSettingsResolver>();

            // Act
            var result = resolver.ResolvePreview(
                new PreviewSettings { InputPath = input, Template = "  news  " });

            // Assert
            Assert.Equal(OperationStatus.Success, result.Status);
            Assert.Null(result.Value!.Clustering.Algorithm);
            Assert.Null(result.Value.Clustering.Language);
            Assert.Equal("news", result.Value.Clustering.Template);
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies template selection rejects either explicit direct-selection option.</summary>
    /// <param name="algorithm">The optional explicit algorithm.</param>
    /// <param name="language">The optional explicit language.</param>
    [Theory]
    [InlineData("Lingo", null)]
    [InlineData(null, "English")]
    public void ResolveProcess_TemplateWithDirectSelection_ReturnsAmbiguousFailure(
        string? algorithm,
        string? language)
    {
        #region implementation

        // Arrange
        var root = createTemporaryDirectory();
        try
        {
            var input = Directory.CreateDirectory(Path.Combine(root, "input")).FullName;
            using var provider = createProvider(
                createConfiguration(
                    new Dictionary<string, string?>
                    {
                        ["CARROTCLI_ENDPOINT"] = "http://localhost:8080/service"
                    }));
            var resolver = provider.GetRequiredService<RunSettingsResolver>();

            // Act
            var result = resolver.ResolveProcess(new ProcessSettings
            {
                InputPath = input,
                Template = "news",
                Algorithm = algorithm,
                Language = language
            });

            // Assert
            Assert.Equal(OperationStatus.Failure, result.Status);
            Assert.Equal("clustering.selection.ambiguous", Assert.Single(result.Messages).Code);
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies preview uses the environment endpoint and configured defaults without prompting.
    /// </summary>
    [Fact]
    public void ResolvePreview_OmittedOptionalSettings_UsesEnvironmentAndDefaults()
    {
        #region implementation

        // Arrange
        var root = createTemporaryDirectory();
        try
        {
            var input = Path.Combine(root, "source.txt");
            File.WriteAllText(input, "Source text");
            var outputPath = Path.Combine(root, "request.json");
            var configuration = createConfiguration(
                new Dictionary<string, string?>
                {
                    ["CARROTCLI_ENDPOINT"] = "http://localhost:8080/service/",
                    ["CarrotCli:DefaultAlgorithm"] = "CustomAlgorithm",
                    ["CarrotCli:DefaultLanguage"] = "CustomLanguage",
                    ["CarrotCli:HttpTimeoutSeconds"] = "75"
                });
            using var provider = createProvider(configuration);
            var resolver = provider.GetRequiredService<RunSettingsResolver>();

            // Act
            var result = resolver.ResolvePreview(
                new PreviewSettings
                {
                    InputPath = input,
                    OutputPath = outputPath,
                    Recursive = true,
                    Overwrite = true
                });

            // Assert
            Assert.Equal(OperationStatus.Success, result.Status);
            var request = Assert.IsType<PreviewRequest>(result.Value);
            Assert.Equal(input, request.InputPath);
            Assert.Equal("http://localhost:8080/service", request.Endpoint.AbsoluteUri.TrimEnd('/'));
            Assert.Equal(outputPath, request.OutputPath);
            Assert.True(request.Recursive);
            Assert.Equal("CustomAlgorithm", request.Clustering.Algorithm);
            Assert.Equal("CustomLanguage", request.Clustering.Language);
            Assert.Null(request.Clustering.Template);
            Assert.Null(request.Clustering.ParametersFile);
            Assert.Equal(TimeSpan.FromSeconds(75), request.Timeout);
            Assert.True(request.Overwrite);
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies explicit endpoint text takes precedence over the configured environment fallback.
    /// </summary>
    [Fact]
    public void ResolveEndpoint_ExplicitEndpoint_OverridesEnvironment()
    {
        #region implementation

        // Arrange
        var configuration = createConfiguration(
            new Dictionary<string, string?>
            {
                ["CARROTCLI_ENDPOINT"] = "https://environment.example/service"
            });
        using var provider = createProvider(configuration);
        var resolver = provider.GetRequiredService<RunSettingsResolver>();

        // Act
        var result = resolver.ResolveEndpoint(
            new EndpointSettings { Endpoint = "https://explicit.example/service", TimeoutSeconds = 1 });

        // Assert
        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal("https://explicit.example/service", result.Value!.AbsoluteUri.TrimEnd('/'));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies the configured environment endpoint is used when the option is absent.
    /// </summary>
    [Fact]
    public void ResolveEndpoint_EndpointOptionAbsent_UsesEnvironment()
    {
        #region implementation

        // Arrange
        var configuration = createConfiguration(
            new Dictionary<string, string?>
            {
                ["CARROTCLI_ENDPOINT"] = "https://environment.example/service/"
            });
        using var provider = createProvider(configuration);
        var resolver = provider.GetRequiredService<RunSettingsResolver>();

        // Act
        var result = resolver.ResolveEndpoint(new EndpointSettings());

        // Assert
        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal("https://environment.example/service", result.Value!.AbsoluteUri.TrimEnd('/'));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies missing endpoint configuration and invalid timeout overrides return stable failures.
    /// </summary>
    /// <param name="scenario">The invalid endpoint-settings scenario.</param>
    /// <param name="expectedCode">The expected machine-readable failure code.</param>
    [Theory]
    [InlineData("missing", "endpoint.missing")]
    [InlineData("explicit-empty", "endpoint.empty")]
    [InlineData("timeout-zero", "timeout.invalid")]
    public void ResolveEndpoint_InvalidSettings_ReturnsStructuredFailure(string scenario, string expectedCode)
    {
        #region implementation

        // Arrange
        var configurationValues = scenario == "explicit-empty"
            ? new Dictionary<string, string?>
            {
                ["CARROTCLI_ENDPOINT"] = "https://environment.example/service"
            }
            : new Dictionary<string, string?>();
        using var provider = createProvider(createConfiguration(configurationValues));
        var resolver = provider.GetRequiredService<RunSettingsResolver>();
        var settings = scenario switch
        {
            "explicit-empty" => new EndpointSettings { Endpoint = "   " },
            "timeout-zero" => new EndpointSettings
            {
                Endpoint = "https://explicit.example/service",
                TimeoutSeconds = 0
            },
            _ => new EndpointSettings()
        };

        // Act
        var result = resolver.ResolveEndpoint(settings);

        // Assert
        Assert.Equal(OperationStatus.Failure, result.Status);
        Assert.Null(result.Value);
        Assert.Equal(expectedCode, Assert.Single(result.Messages).Code);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies invalid process paths, timeouts, and colliding path options fail before execution.
    /// </summary>
    /// <param name="scenario">The invalid process setting class.</param>
    /// <param name="expectedCode">The expected machine-readable failure code.</param>
    [Theory]
    [InlineData("missing-input", "input.path.missing")]
    [InlineData("invalid-timeout", "timeout.invalid")]
    [InlineData("missing-output-parent", "output.path.parent")]
    [InlineData("missing-log-parent", "log.path.parent")]
    [InlineData("directory-log-path", "log.path.directory")]
    [InlineData("missing-parameters", "parameters.path.missing")]
    [InlineData("path-conflict", "paths.conflict")]
    public void ResolveProcess_InvalidSettings_ReturnsStructuredFailure(string scenario, string expectedCode)
    {
        #region implementation

        // Arrange
        var root = createTemporaryDirectory();
        try
        {
            var input = Directory.CreateDirectory(Path.Combine(root, "input")).FullName;
            var missingInput = Path.Combine(root, "missing-input");
            var missingOutput = Path.Combine(root, "missing-parent", "report.xlsx");
            var missingLog = Path.Combine(root, "missing-log-parent", "run.log");
            var missingParameters = Path.Combine(root, "missing.json");
            var sharedPath = Path.Combine(root, "shared.out");
            using var provider = createProvider(
                createConfiguration(
                    new Dictionary<string, string?>
                    {
                        ["CARROTCLI_ENDPOINT"] = "http://localhost:8080/service"
                    }));
            var resolver = provider.GetRequiredService<RunSettingsResolver>();
            var settings = scenario switch
            {
                "missing-input" => new ProcessSettings { InputPath = missingInput },
                "invalid-timeout" => new ProcessSettings { InputPath = input, TimeoutSeconds = -1 },
                "missing-output-parent" => new ProcessSettings { InputPath = input, OutputPath = missingOutput },
                "missing-log-parent" => new ProcessSettings { InputPath = input, LogFile = missingLog },
                "directory-log-path" => new ProcessSettings { InputPath = input, LogFile = root },
                "missing-parameters" => new ProcessSettings { InputPath = input, ParametersFile = missingParameters },
                "path-conflict" => new ProcessSettings
                {
                    InputPath = input,
                    OutputPath = sharedPath,
                    LogFile = sharedPath
                },
                _ => throw new InvalidOperationException("Unsupported invalid-settings fixture.")
            };

            // Act
            var result = resolver.ResolveProcess(settings);

            // Assert
            Assert.Equal(OperationStatus.Failure, result.Status);
            Assert.Equal(expectedCode, Assert.Single(result.Messages).Code);
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies preview output cannot overwrite the parameter file used to create the request.
    /// </summary>
    [Fact]
    public void ResolvePreview_OutputMatchesParametersFile_ReturnsPathConflict()
    {
        #region implementation

        // Arrange
        var root = createTemporaryDirectory();
        try
        {
            var input = Directory.CreateDirectory(Path.Combine(root, "input")).FullName;
            var parametersFile = Path.Combine(root, "parameters.json");
            File.WriteAllText(parametersFile, "{}");
            using var provider = createProvider(
                createConfiguration(
                    new Dictionary<string, string?>
                    {
                        ["CARROTCLI_ENDPOINT"] = "http://localhost:8080/service"
                    }));
            var resolver = provider.GetRequiredService<RunSettingsResolver>();

            // Act
            var result = resolver.ResolvePreview(
                new PreviewSettings
                {
                    InputPath = input,
                    ParametersFile = parametersFile,
                    OutputPath = parametersFile
                });

            // Assert
            Assert.Equal(OperationStatus.Failure, result.Status);
            Assert.Equal("paths.conflict", Assert.Single(result.Messages).Code);
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies all resolver entry points reject null settings as programmer faults.
    /// </summary>
    [Fact]
    public void ResolveMethods_NullSettings_ThrowArgumentNullException()
    {
        #region implementation

        // Arrange
        using var provider = createProvider(createConfiguration(new Dictionary<string, string?>()));
        var resolver = provider.GetRequiredService<RunSettingsResolver>();

        // Act and assert
        Assert.Throws<ArgumentNullException>(() => resolver.ResolveProcess(null!));
        Assert.Throws<ArgumentNullException>(() => resolver.ResolvePreview(null!));
        Assert.Throws<ArgumentNullException>(() => resolver.ResolveEndpoint(null!));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Creates one in-memory configuration with environment-style and options values.
    /// </summary>
    /// <param name="values">The configuration key/value pairs.</param>
    /// <returns>The immutable configuration root.</returns>
    private static IConfiguration createConfiguration(IReadOnlyDictionary<string, string?> values)
    {
        #region implementation

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Builds the production registration graph for direct resolver coverage.
    /// </summary>
    /// <param name="configuration">The test configuration.</param>
    /// <returns>The disposable production service provider.</returns>
    private static ServiceProvider createProvider(IConfiguration configuration)
    {
        #region implementation

        return new ServiceCollection()
            .AddCarrotCli(configuration)
            .BuildServiceProvider();

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one isolated filesystem directory for resolver tests.</summary>
    /// <returns>The absolute owned directory path.</returns>
    private static string createTemporaryDirectory()
    {
        #region implementation

        var path = Path.Combine(Path.GetTempPath(), $"carrot-run-settings-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;

        #endregion
    }

    /**************************************************************/
    /// <summary>Deletes one exact owned resolver-test directory.</summary>
    /// <param name="path">The owned directory.</param>
    private static void deleteTemporaryDirectory(string path)
    {
        #region implementation

        var fullPath = Path.GetFullPath(path);
        if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, recursive: true);
        }

        #endregion
    }

    #endregion
}
