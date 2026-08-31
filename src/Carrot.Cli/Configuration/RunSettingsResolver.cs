using Carrot.Cli.CarrotApi;
using Carrot.Cli.Cli.Settings;
using Carrot.Cli.Common;
using Carrot.Cli.Input;
using Carrot.Cli.Processing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Carrot.Cli.Configuration;

/**************************************************************/
/// <summary>
/// Resolves command settings, environment endpoint fallback, defaults, and validated run requests.
/// </summary>
/// <remarks>
/// Noninteractive endpoint precedence is the explicit option followed by
/// <c>CARROTCLI_ENDPOINT</c>. Missing endpoints produce expected configuration failures.
/// This boundary never prompts and therefore remains safe for scheduled and redirected execution.
/// </remarks>
/// <seealso cref="ProcessRequest"/>
/// <seealso cref="PreviewRequest"/>
internal sealed class RunSettingsResolver
{
    #region implementation

    private readonly IConfiguration _configuration;
    private readonly EndpointResolver _endpointResolver;
    private readonly InputPathNormalizer _inputPathNormalizer;
    private readonly InputSourceResolver _inputSourceResolver;
    private readonly CarrotCliOptions _options;

    /**************************************************************/
    /// <summary>
    /// Initializes the noninteractive settings boundary with validated defaults and focused resolvers.
    /// </summary>
    /// <param name="options">The validated application defaults.</param>
    /// <param name="configuration">The layered configuration containing environment-variable values.</param>
    /// <param name="endpointResolver">The strict Carrot service endpoint resolver.</param>
    /// <param name="inputPathNormalizer">The shared absolute input-path normalizer.</param>
    /// <param name="inputSourceResolver">The supported input source classifier.</param>
    /// <exception cref="ArgumentNullException">Thrown when a required dependency is null.</exception>
    /// <seealso cref="CarrotCliOptions"/>
    /// <seealso cref="EndpointResolver"/>
    public RunSettingsResolver(
        IOptions<CarrotCliOptions> options,
        IConfiguration configuration,
        EndpointResolver endpointResolver,
        InputPathNormalizer inputPathNormalizer,
        InputSourceResolver inputSourceResolver)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(endpointResolver);
        ArgumentNullException.ThrowIfNull(inputPathNormalizer);
        ArgumentNullException.ThrowIfNull(inputSourceResolver);

        _options = options.Value;
        _configuration = configuration;
        _endpointResolver = endpointResolver;
        _inputPathNormalizer = inputPathNormalizer;
        _inputSourceResolver = inputSourceResolver;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Resolves complete processing settings into an immutable workflow request.
    /// </summary>
    /// <param name="settings">The command settings supplied by Spectre.</param>
    /// <returns>A successful request or structured configuration messages.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is null.</exception>
    /// <seealso cref="ProcessRequest"/>
    internal OperationResult<ProcessRequest> ResolveProcess(ProcessSettings settings)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(settings);

        var commonResult = resolveClusteringSettings(settings);
        if (commonResult.Status == OperationStatus.Failure)
        {
            return OperationResult<ProcessRequest>.Failure(commonResult.Messages);
        }

        string? outputPath = null;
        if (settings.OutputPath is not null)
        {
            var outputResult = resolveOutputPath(settings.OutputPath);
            if (outputResult.Status == OperationStatus.Failure)
            {
                return OperationResult<ProcessRequest>.Failure(outputResult.Messages);
            }

            outputPath = outputResult.Value!;
        }

        string? logFile = null;
        if (settings.LogFile is not null)
        {
            var logResult = resolveLogPath(settings.LogFile);
            if (logResult.Status == OperationStatus.Failure)
            {
                return OperationResult<ProcessRequest>.Failure(logResult.Messages);
            }

            logFile = logResult.Value!;
        }

        var common = commonResult.Value!;
        var conflictResult = validateDistinctPaths(outputPath, logFile, common.ParametersFile);
        if (conflictResult is not null)
        {
            return OperationResult<ProcessRequest>.Failure([conflictResult]);
        }

        return OperationResult<ProcessRequest>.Success(
            new ProcessRequest
            {
                InputPath = common.InputPath,
                Endpoint = common.Endpoint,
                OutputPath = outputPath,
                Recursive = settings.Recursive,
                Algorithm = common.Algorithm,
                Language = common.Language,
                Template = common.Template,
                ParametersFile = common.ParametersFile,
                Timeout = common.Timeout,
                Overwrite = settings.Overwrite,
                WriteJsonArtifacts = !settings.NoJsonArtifacts,
                Quiet = settings.Quiet,
                LogFile = logFile
            });

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Resolves preview settings into an immutable workflow request.
    /// </summary>
    /// <param name="settings">The command settings supplied by Spectre.</param>
    /// <returns>A successful request or structured configuration messages.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is null.</exception>
    /// <seealso cref="PreviewRequest"/>
    internal OperationResult<PreviewRequest> ResolvePreview(PreviewSettings settings)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(settings);

        var commonResult = resolveClusteringSettings(settings);
        if (commonResult.Status == OperationStatus.Failure)
        {
            return OperationResult<PreviewRequest>.Failure(commonResult.Messages);
        }

        string? outputPath = null;
        if (settings.OutputPath is not null)
        {
            var outputResult = resolveOutputPath(settings.OutputPath);
            if (outputResult.Status == OperationStatus.Failure)
            {
                return OperationResult<PreviewRequest>.Failure(outputResult.Messages);
            }

            outputPath = outputResult.Value!;
        }

        var common = commonResult.Value!;
        if (pathsEqual(outputPath, common.ParametersFile))
        {
            return failure<PreviewRequest>(
                "paths.conflict",
                "The output path and parameters file must identify different files.");
        }

        return OperationResult<PreviewRequest>.Success(
            new PreviewRequest
            {
                InputPath = common.InputPath,
                Endpoint = common.Endpoint,
                OutputPath = outputPath,
                Recursive = settings.Recursive,
                Algorithm = common.Algorithm,
                Language = common.Language,
                Template = common.Template,
                ParametersFile = common.ParametersFile,
                Timeout = common.Timeout,
                Overwrite = settings.Overwrite
            });

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Resolves endpoint settings using command-line then environment precedence.
    /// </summary>
    /// <remarks>
    /// The timeout is validated with the endpoint even though this method returns only the URI.
    /// This keeps a future <c>server-info</c> command from accepting a partially valid settings object.
    /// </remarks>
    /// <param name="settings">The endpoint settings supplied by Spectre.</param>
    /// <returns>A successful absolute service URI or structured configuration messages.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is null.</exception>
    internal OperationResult<Uri> ResolveEndpoint(EndpointSettings settings)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(settings);

        var endpointResult = resolveEndpoint(settings.Endpoint);
        if (endpointResult.Status == OperationStatus.Failure)
        {
            return endpointResult;
        }

        var timeoutResult = resolveTimeout(settings.TimeoutSeconds);
        return timeoutResult.Status == OperationStatus.Failure
            ? OperationResult<Uri>.Failure(timeoutResult.Messages)
            : endpointResult;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Resolves settings shared by processing and preview requests.
    /// </summary>
    /// <param name="settings">The common clustering settings.</param>
    /// <returns>A successful normalized settings value or one structured failure.</returns>
    private OperationResult<ResolvedClusteringSettings> resolveClusteringSettings(ClusteringSettings settings)
    {
        #region implementation

        var inputResult = _inputPathNormalizer.Normalize(settings.InputPath);
        if (inputResult.Status == OperationStatus.Failure)
        {
            return OperationResult<ResolvedClusteringSettings>.Failure(inputResult.Messages);
        }

        var inputPath = inputResult.Value!;
        var sourceResult = _inputSourceResolver.Resolve(inputPath);
        if (sourceResult.Status == OperationStatus.Failure)
        {
            return OperationResult<ResolvedClusteringSettings>.Failure(sourceResult.Messages);
        }

        var endpointResult = resolveEndpoint(settings.Endpoint);
        if (endpointResult.Status == OperationStatus.Failure)
        {
            return OperationResult<ResolvedClusteringSettings>.Failure(endpointResult.Messages);
        }

        var timeoutResult = resolveTimeout(settings.TimeoutSeconds);
        if (timeoutResult.Status == OperationStatus.Failure)
        {
            return OperationResult<ResolvedClusteringSettings>.Failure(timeoutResult.Messages);
        }

        var algorithmResult = resolveRequiredName(
            settings.Algorithm,
            _options.DefaultAlgorithm,
            "clustering.algorithm.empty",
            "The clustering algorithm must not be empty.");
        if (algorithmResult.Status == OperationStatus.Failure)
        {
            return OperationResult<ResolvedClusteringSettings>.Failure(algorithmResult.Messages);
        }

        var languageResult = resolveRequiredName(
            settings.Language,
            _options.DefaultLanguage,
            "clustering.language.empty",
            "The clustering language must not be empty.");
        if (languageResult.Status == OperationStatus.Failure)
        {
            return OperationResult<ResolvedClusteringSettings>.Failure(languageResult.Messages);
        }

        string? template = null;
        if (settings.Template is not null)
        {
            template = settings.Template.Trim();
            if (template.Length == 0)
            {
                return failure<ResolvedClusteringSettings>(
                    "clustering.template.empty",
                    "The clustering template must not be empty when supplied.");
            }
        }

        string? parametersFile = null;
        if (settings.ParametersFile is not null)
        {
            var parametersResult = resolveParametersFile(settings.ParametersFile);
            if (parametersResult.Status == OperationStatus.Failure)
            {
                return OperationResult<ResolvedClusteringSettings>.Failure(parametersResult.Messages);
            }

            parametersFile = parametersResult.Value!;
        }

        return OperationResult<ResolvedClusteringSettings>.Success(
            new ResolvedClusteringSettings
            {
                InputPath = inputPath,
                Endpoint = endpointResult.Value!,
                Algorithm = algorithmResult.Value!,
                Language = languageResult.Value!,
                Template = template,
                ParametersFile = parametersFile,
                Timeout = timeoutResult.Value
            });

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Applies explicit-option and environment-configuration endpoint precedence.
    /// </summary>
    /// <param name="explicitEndpoint">The endpoint option, including an explicitly empty value.</param>
    /// <returns>A normalized endpoint or a structured missing/invalid endpoint failure.</returns>
    private OperationResult<Uri> resolveEndpoint(string? explicitEndpoint)
    {
        #region implementation

        var configuredEndpoint = explicitEndpoint is null
            ? _configuration["CARROTCLI_ENDPOINT"]
            : explicitEndpoint;

        if (explicitEndpoint is null && string.IsNullOrWhiteSpace(configuredEndpoint))
        {
            return failure<Uri>(
                "endpoint.missing",
                "Supply --endpoint or set CARROTCLI_ENDPOINT for noninteractive commands.");
        }

        return _endpointResolver.Resolve(configuredEndpoint);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Resolves an optional positive timeout override or the validated configured default.
    /// </summary>
    /// <param name="timeoutSeconds">The optional command-line timeout in seconds.</param>
    /// <returns>A positive timeout or a structured configuration failure.</returns>
    private OperationResult<TimeSpan> resolveTimeout(int? timeoutSeconds)
    {
        #region implementation

        var resolvedSeconds = timeoutSeconds ?? _options.HttpTimeoutSeconds;
        return resolvedSeconds <= 0
            ? failure<TimeSpan>("timeout.invalid", "The timeout must be greater than zero seconds.")
            : OperationResult<TimeSpan>.Success(TimeSpan.FromSeconds(resolvedSeconds));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Resolves one explicit clustering name or its validated application default.
    /// </summary>
    /// <param name="explicitValue">The optional explicit command value.</param>
    /// <param name="defaultValue">The validated configured default.</param>
    /// <param name="failureCode">The stable empty-value failure code.</param>
    /// <param name="failureMessage">The safe empty-value failure message.</param>
    /// <returns>The trimmed explicit value or default.</returns>
    private static OperationResult<string> resolveRequiredName(
        string? explicitValue,
        string defaultValue,
        string failureCode,
        string failureMessage)
    {
        #region implementation

        var resolvedValue = (explicitValue ?? defaultValue).Trim();
        return resolvedValue.Length == 0
            ? failure<string>(failureCode, failureMessage)
            : OperationResult<string>.Success(resolvedValue);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Normalizes a process or preview output path without imposing later artifact-type rules.
    /// </summary>
    /// <param name="outputPath">The file or existing directory destination.</param>
    /// <returns>The normalized path or a structured path failure.</returns>
    private static OperationResult<string> resolveOutputPath(string outputPath)
    {
        #region implementation

        var pathResult = normalizePath(outputPath, "output.path", "output path");
        if (pathResult.Status == OperationStatus.Failure)
        {
            return pathResult;
        }

        var fullPath = pathResult.Value!;
        if (!Directory.Exists(fullPath) && !File.Exists(fullPath))
        {
            var parentDirectory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(parentDirectory) || !Directory.Exists(parentDirectory))
            {
                return failure<string>("output.path.parent", "The output path parent directory does not exist.");
            }
        }

        return OperationResult<string>.Success(fullPath);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Normalizes a log file and requires its parent directory to exist.
    /// </summary>
    /// <param name="logFile">The diagnostic log path.</param>
    /// <returns>The normalized file path or a structured path failure.</returns>
    private static OperationResult<string> resolveLogPath(string logFile)
    {
        #region implementation

        var pathResult = normalizePath(logFile, "log.path", "log file path");
        if (pathResult.Status == OperationStatus.Failure)
        {
            return pathResult;
        }

        var fullPath = pathResult.Value!;
        if (Directory.Exists(fullPath))
        {
            return failure<string>("log.path.directory", "The log path must name a file, not a directory.");
        }

        var parentDirectory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(parentDirectory) || !Directory.Exists(parentDirectory))
        {
            return failure<string>("log.path.parent", "The log file parent directory does not exist.");
        }

        return OperationResult<string>.Success(fullPath);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Normalizes a parameter-file path and validates only its file-system shape.
    /// </summary>
    /// <remarks>
    /// JSON size, encoding, root shape, and contents remain the responsibility of the clustering
    /// configuration milestone. This foundation proves only that an existing <c>.json</c> file was selected.
    /// </remarks>
    /// <param name="parametersFile">The JSON parameter-file path.</param>
    /// <returns>The normalized file path or a structured path failure.</returns>
    private static OperationResult<string> resolveParametersFile(string parametersFile)
    {
        #region implementation

        var pathResult = normalizePath(parametersFile, "parameters.path", "parameters file path");
        if (pathResult.Status == OperationStatus.Failure)
        {
            return pathResult;
        }

        var fullPath = pathResult.Value!;
        if (Directory.Exists(fullPath))
        {
            return failure<string>(
                "parameters.path.directory",
                "The parameters path must name a JSON file, not a directory.");
        }

        if (!string.Equals(Path.GetExtension(fullPath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            return failure<string>("parameters.path.extension", "The parameters file must end in .json.");
        }

        if (!File.Exists(fullPath))
        {
            return failure<string>("parameters.path.missing", $"Parameters file not found: {fullPath}");
        }

        return OperationResult<string>.Success(fullPath);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Trims, unquotes, and converts one command path to an absolute path.
    /// </summary>
    /// <param name="input">The raw command option value.</param>
    /// <param name="codePrefix">The stable diagnostic-code prefix.</param>
    /// <param name="description">The human-readable path role.</param>
    /// <returns>A normalized absolute path or a structured syntax failure.</returns>
    private static OperationResult<string> normalizePath(string input, string codePrefix, string description)
    {
        #region implementation

        var value = input.Trim();
        if (value.Length == 0)
        {
            return failure<string>($"{codePrefix}.empty", $"The {description} must not be empty.");
        }

        var beginsWithQuote = value[0] is '\'' or '"';
        var endsWithQuote = value[^1] is '\'' or '"';
        if (beginsWithQuote || endsWithQuote)
        {
            if (!beginsWithQuote || !endsWithQuote || value.Length < 2 || value[0] != value[^1])
            {
                return failure<string>(
                    $"{codePrefix}.quotes",
                    $"Quoted {description} values must use matching surrounding quotes.");
            }

            value = value[1..^1].Trim();
            if (value.Length == 0)
            {
                return failure<string>($"{codePrefix}.empty", $"The quoted {description} must not be empty.");
            }
        }

        try
        {
            return OperationResult<string>.Success(Path.GetFullPath(value));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return failure<string>($"{codePrefix}.invalid", $"The {description} is invalid: {exception.Message}");
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Rejects collisions among process output, log, and parameter files.
    /// </summary>
    /// <param name="outputPath">The optional normalized output path.</param>
    /// <param name="logFile">The optional normalized log path.</param>
    /// <param name="parametersFile">The optional normalized parameter-file path.</param>
    /// <returns>A conflict message when any two file roles collide; otherwise <see langword="null"/>.</returns>
    private static OperationMessage? validateDistinctPaths(
        string? outputPath,
        string? logFile,
        string? parametersFile)
    {
        #region implementation

        if (pathsEqual(outputPath, logFile))
        {
            return createError("paths.conflict", "The output path and log file must identify different files.");
        }

        if (pathsEqual(outputPath, parametersFile))
        {
            return createError("paths.conflict", "The output path and parameters file must identify different files.");
        }

        return pathsEqual(logFile, parametersFile)
            ? createError("paths.conflict", "The log file and parameters file must identify different files.")
            : null;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Compares two normalized paths using the current operating system's path case rules.
    /// </summary>
    /// <param name="left">The first optional normalized path.</param>
    /// <param name="right">The second optional normalized path.</param>
    /// <returns><see langword="true"/> only when both paths are present and identify the same location.</returns>
    private static bool pathsEqual(string? left, string? right)
    {
        #region implementation

        if (left is null || right is null)
        {
            return false;
        }

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(left, right, comparison);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Creates one immutable error message with a stable code.
    /// </summary>
    /// <param name="code">The machine-readable diagnostic code.</param>
    /// <param name="message">The safe human-readable diagnostic.</param>
    /// <returns>The immutable error message.</returns>
    private static OperationMessage createError(string code, string message)
    {
        #region implementation

        return new OperationMessage
        {
            Code = code,
            Message = message,
            Severity = OperationMessageSeverity.Error
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Creates one failed operation result containing one safe configuration message.
    /// </summary>
    /// <typeparam name="T">The absent successful value type.</typeparam>
    /// <param name="code">The stable diagnostic code.</param>
    /// <param name="message">The safe human-readable diagnostic.</param>
    /// <returns>A failed operation result.</returns>
    private static OperationResult<T> failure<T>(string code, string message)
    {
        #region implementation

        return OperationResult<T>.Failure([createError(code, message)]);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Carries normalized settings shared by process and preview requests during resolution.
    /// </summary>
    private sealed record ResolvedClusteringSettings
    {
        #region implementation

        /**************************************************************/
        /// <summary>Gets the normalized and supported input path.</summary>
        public required string InputPath { get; init; }

        /**************************************************************/
        /// <summary>Gets the normalized Carrot service endpoint.</summary>
        public required Uri Endpoint { get; init; }

        /**************************************************************/
        /// <summary>Gets the explicit or configured algorithm name.</summary>
        public required string Algorithm { get; init; }

        /**************************************************************/
        /// <summary>Gets the explicit or configured language name.</summary>
        public required string Language { get; init; }

        /**************************************************************/
        /// <summary>Gets the optional trimmed template name.</summary>
        public string? Template { get; init; }

        /**************************************************************/
        /// <summary>Gets the optional normalized existing JSON parameter-file path.</summary>
        public string? ParametersFile { get; init; }

        /**************************************************************/
        /// <summary>Gets the positive explicit or configured timeout.</summary>
        public TimeSpan Timeout { get; init; }

        #endregion
    }

    #endregion
}
