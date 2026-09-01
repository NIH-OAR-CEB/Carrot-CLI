using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using Carrot.Cli.Common;
using Microsoft.Extensions.Options;

namespace Carrot.Cli.Configuration;

/**************************************************************/
/// <summary>
/// Resolves defaults, template precedence, and bounded JSON parameter files into one configuration.
/// </summary>
/// <remarks>
/// Expected caller and parameter-file failures are returned as structured operation messages.
/// Caller cancellation remains exceptional so orchestration can map it consistently to exit code 130.
/// </remarks>
/// <seealso cref="ClusteringSelection"/>
/// <seealso cref="ClusteringConfiguration"/>
internal sealed class ClusteringConfigurationResolver
{
    #region implementation

    private const int FileReadBufferSize = 81_920;

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private readonly CarrotCliOptions _options;

    /**************************************************************/
    /// <summary>Initializes the resolver with validated clustering defaults and the parameter-file limit.</summary>
    /// <param name="options">The validated application configuration.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    public ClusteringConfigurationResolver(IOptions<CarrotCliOptions> options)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;

        #endregion
    }

    /**************************************************************/
    /// <summary>Resolves one clustering selection and loads any bounded JSON parameter object.</summary>
    /// <param name="selection">The optional algorithm, language, template, and parameter-file selections.</param>
    /// <param name="cancellationToken">The token signaling cooperative parameter-file cancellation.</param>
    /// <returns>A resolved configuration or one structured expected failure.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="selection"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when cancellation is requested.</exception>
    public async Task<OperationResult<ClusteringConfiguration>> ResolveAsync(
        ClusteringSelection selection,
        CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(selection);
        cancellationToken.ThrowIfCancellationRequested();

        var algorithmResult = resolveOptionalName(
            selection.Algorithm,
            "clustering.algorithm.empty",
            "The clustering algorithm must not be empty when supplied.");
        if (algorithmResult.Status == OperationStatus.Failure)
        {
            return OperationResult<ClusteringConfiguration>.Failure(algorithmResult.Messages);
        }

        var languageResult = resolveOptionalName(
            selection.Language,
            "clustering.language.empty",
            "The clustering language must not be empty when supplied.");
        if (languageResult.Status == OperationStatus.Failure)
        {
            return OperationResult<ClusteringConfiguration>.Failure(languageResult.Messages);
        }

        var templateResult = resolveOptionalName(
            selection.Template,
            "clustering.template.empty",
            "The clustering template must not be empty when supplied.");
        if (templateResult.Status == OperationStatus.Failure)
        {
            return OperationResult<ClusteringConfiguration>.Failure(templateResult.Messages);
        }

        var algorithm = algorithmResult.Value;
        var language = languageResult.Value;
        var template = templateResult.Value;
        if (template is not null && (algorithm is not null || language is not null))
        {
            return failure<ClusteringConfiguration>(
                "clustering.selection.ambiguous",
                "A template cannot be combined with an explicit algorithm or language.");
        }

        if (template is null)
        {
            algorithm = algorithm ?? _options.DefaultAlgorithm.Trim();
            language = language ?? _options.DefaultLanguage.Trim();
            if (algorithm.Length == 0)
            {
                return failure<ClusteringConfiguration>(
                    "clustering.algorithm.empty",
                    "The configured clustering algorithm must not be empty.");
            }

            if (language.Length == 0)
            {
                return failure<ClusteringConfiguration>(
                    "clustering.language.empty",
                    "The configured clustering language must not be empty.");
            }
        }

        IReadOnlyDictionary<string, JsonElement>? parameters = null;
        if (selection.ParametersFile is not null)
        {
            var parameterResult = await readParametersAsync(
                selection.ParametersFile,
                cancellationToken).ConfigureAwait(false);
            if (parameterResult.Status == OperationStatus.Failure)
            {
                return OperationResult<ClusteringConfiguration>.Failure(parameterResult.Messages);
            }

            parameters = parameterResult.Value;
        }

        return OperationResult<ClusteringConfiguration>.Success(new ClusteringConfiguration
        {
            Algorithm = algorithm,
            Language = language,
            Template = template,
            Parameters = parameters
        });

        #endregion
    }

    /**************************************************************/
    /// <summary>Trims an optional exact identifier and rejects an explicitly empty value.</summary>
    /// <param name="value">The optional caller-supplied identifier.</param>
    /// <param name="failureCode">The stable empty-value error code.</param>
    /// <param name="failureMessage">The safe empty-value diagnostic.</param>
    /// <returns>The trimmed value, a null value, or a structured failure.</returns>
    private static OptionalNameResult resolveOptionalName(
        string? value,
        string failureCode,
        string failureMessage)
    {
        #region implementation

        if (value is null)
        {
            return OptionalNameResult.Success(null);
        }

        var trimmedValue = value.Trim();
        return trimmedValue.Length == 0
            ? OptionalNameResult.Failure(createError(failureCode, failureMessage))
            : OptionalNameResult.Success(trimmedValue);

        #endregion
    }

    /**************************************************************/
    /// <summary>Reads and parses one bounded UTF-8 JSON object without retaining the source document.</summary>
    /// <param name="parametersFile">The caller-supplied parameter-file path.</param>
    /// <param name="cancellationToken">The token signaling cooperative file-read cancellation.</param>
    /// <returns>Detached parameter values or one structured file or JSON failure.</returns>
    private async Task<OperationResult<IReadOnlyDictionary<string, JsonElement>>> readParametersAsync(
        string parametersFile,
        CancellationToken cancellationToken)
    {
        #region implementation

        var pathResult = resolveParameterPath(parametersFile);
        if (pathResult.Status == OperationStatus.Failure)
        {
            return OperationResult<IReadOnlyDictionary<string, JsonElement>>.Failure(pathResult.Messages);
        }

        var fullPath = pathResult.Value!;
        try
        {
            await using var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                FileReadBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (stream.Length > _options.MaximumParameterFileBytes)
            {
                return parameterFileTooLarge();
            }

            using var content = new MemoryStream((int)stream.Length);
            var buffer = new byte[FileReadBufferSize];
            var totalBytes = 0;
            while (true)
            {
                var bytesRead = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    break;
                }

                // Check each read as well as the initial length so a concurrently growing file
                // cannot bypass the configured memory and request-size safeguard.
                if (totalBytes > _options.MaximumParameterFileBytes - bytesRead)
                {
                    return parameterFileTooLarge();
                }

                await content.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                totalBytes += bytesRead;
            }

            // JsonDocument tolerates malformed byte sequences by replacing them, but parameter
            // files are required to be valid UTF-8 so invalid source bytes cannot change values.
            _ = StrictUtf8.GetCharCount(content.GetBuffer(), 0, totalBytes);
            content.Position = 0;
            using var document = await JsonDocument.ParseAsync(
                content,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return failure<IReadOnlyDictionary<string, JsonElement>>(
                    "clustering.parameters.root",
                    "The parameters file root must be a JSON object.");
            }

            var parameters = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!parameters.TryAdd(property.Name, property.Value.Clone()))
                {
                    return failure<IReadOnlyDictionary<string, JsonElement>>(
                        "clustering.parameters.duplicate",
                        $"The parameters file contains the duplicate property '{property.Name}'.");
                }
            }

            return OperationResult<IReadOnlyDictionary<string, JsonElement>>.Success(
                new ReadOnlyDictionary<string, JsonElement>(parameters));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException)
        {
            return failure<IReadOnlyDictionary<string, JsonElement>>(
                "clustering.parameters.invalid-json",
                "The parameters file must contain valid UTF-8 JSON.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return failure<IReadOnlyDictionary<string, JsonElement>>(
                "clustering.parameters.unreadable",
                $"The parameters file could not be read: {fullPath}");
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Normalizes and validates the basic file-system shape of one parameter path.</summary>
    /// <param name="parametersFile">The raw path supplied by a caller.</param>
    /// <returns>An absolute existing JSON file path or one structured failure.</returns>
    private static OperationResult<string> resolveParameterPath(string parametersFile)
    {
        #region implementation

        if (string.IsNullOrWhiteSpace(parametersFile))
        {
            return failure<string>(
                "clustering.parameters.path-empty",
                "The parameters file path must not be empty.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(parametersFile.Trim());
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return failure<string>(
                "clustering.parameters.path-invalid",
                "The parameters file path is invalid.");
        }

        if (!string.Equals(Path.GetExtension(fullPath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            return failure<string>(
                "clustering.parameters.extension",
                "The parameters file must end in .json.");
        }

        if (!File.Exists(fullPath))
        {
            return failure<string>(
                "clustering.parameters.missing",
                $"Parameters file not found: {fullPath}");
        }

        return OperationResult<string>.Success(fullPath);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates the standard parameter-file size-limit failure.</summary>
    /// <returns>A failed parameter dictionary result.</returns>
    private OperationResult<IReadOnlyDictionary<string, JsonElement>> parameterFileTooLarge()
    {
        #region implementation

        return failure<IReadOnlyDictionary<string, JsonElement>>(
            "clustering.parameters.too-large",
            $"The parameters file must not exceed {_options.MaximumParameterFileBytes:N0} bytes.");

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one immutable error message.</summary>
    /// <param name="code">The stable machine-readable code.</param>
    /// <param name="message">The safe user-facing diagnostic.</param>
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
    /// <summary>Creates one failed operation result containing one error.</summary>
    /// <typeparam name="T">The absent successful value type.</typeparam>
    /// <param name="code">The stable machine-readable code.</param>
    /// <param name="message">The safe user-facing diagnostic.</param>
    /// <returns>A failed operation result.</returns>
    private static OperationResult<T> failure<T>(string code, string message)
    {
        #region implementation

        return OperationResult<T>.Failure([createError(code, message)]);

        #endregion
    }

    /**************************************************************/
    /// <summary>Carries an optional normalized name without forcing a generic result to hold null.</summary>
    private sealed record OptionalNameResult
    {
        #region implementation

        /**************************************************************/
        /// <summary>Gets the optional normalized value.</summary>
        internal string? Value { get; private init; }

        /**************************************************************/
        /// <summary>Gets the outcome status.</summary>
        internal OperationStatus Status { get; private init; }

        /**************************************************************/
        /// <summary>Gets the failure messages.</summary>
        internal IReadOnlyList<OperationMessage> Messages { get; private init; } = Array.Empty<OperationMessage>();

        /**************************************************************/
        /// <summary>Creates a successful optional name.</summary>
        /// <param name="value">The optional normalized value.</param>
        /// <returns>A successful result.</returns>
        internal static OptionalNameResult Success(string? value)
        {
            #region implementation

            return new OptionalNameResult { Status = OperationStatus.Success, Value = value };

            #endregion
        }

        /**************************************************************/
        /// <summary>Creates a failed optional name.</summary>
        /// <param name="message">The single validation failure.</param>
        /// <returns>A failed result.</returns>
        internal static OptionalNameResult Failure(OperationMessage message)
        {
            #region implementation

            return new OptionalNameResult { Status = OperationStatus.Failure, Messages = [message] };

            #endregion
        }

        #endregion
    }

    #endregion
}
