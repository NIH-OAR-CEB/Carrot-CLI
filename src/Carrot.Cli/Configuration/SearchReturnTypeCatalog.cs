using Carrot.Cli.Common;
using Microsoft.Extensions.Configuration;

namespace Carrot.Cli.Configuration;

/**************************************************************/
/// <summary>
/// Loads and validates the configured iSearch return datasets from application configuration.
/// </summary>
/// <remarks>
/// The catalog is intentionally feature-local: malformed return configuration is reported when
/// iSearch is entered and does not prevent unrelated CLI commands from starting. Configuration
/// child order and field order are retained for predictable menus and requests.
/// </remarks>
/// <seealso cref="SearchReturnTypeDefinition"/>
internal sealed class SearchReturnTypeCatalog
{
    #region implementation

    private readonly OperationResult<IReadOnlyList<SearchReturnTypeDefinition>> _result;

    /**************************************************************/
    /// <summary>Initializes the catalog from the <c>iSearchReturnTypes</c> configuration section.</summary>
    /// <param name="configuration">The layered application configuration.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configuration"/> is <see langword="null"/>.</exception>
    public SearchReturnTypeCatalog(IConfiguration configuration)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(configuration);
        _result = load(configuration.GetSection("iSearchReturnTypes"));

        #endregion
    }

    /**************************************************************/
    /// <summary>Gets the validated return datasets or safe configuration diagnostics.</summary>
    /// <returns>The ordered return definitions on success, or messages describing invalid configuration.</returns>
    public OperationResult<IReadOnlyList<SearchReturnTypeDefinition>> GetDefinitions()
    {
        #region implementation

        return _result;

        #endregion
    }

    /**************************************************************/
    /// <summary>Builds an immutable return-dataset result from one configuration section.</summary>
    /// <param name="section">The <c>iSearchReturnTypes</c> section.</param>
    /// <returns>A validated ordered catalog or accumulated configuration failures.</returns>
    private static OperationResult<IReadOnlyList<SearchReturnTypeDefinition>> load(IConfigurationSection section)
    {
        #region implementation

        var groups = section.GetChildren().ToArray();
        if (groups.Length == 0)
        {
            return failure("isearch.return-types.missing", "No iSearch return datasets are configured under iSearchReturnTypes.");
        }

        var definitions = new List<SearchReturnTypeDefinition>(groups.Length);
        var messages = new List<OperationMessage>();
        foreach (var group in groups)
        {
            if (string.IsNullOrWhiteSpace(group.Key))
            {
                messages.Add(message(
                    "isearch.return-type.name-empty",
                    "An iSearch return dataset has an empty configuration name."));
                continue;
            }

            var fields = group.GetSection("DefaultFields")
                .GetChildren()
                .Select(item => item.Value?.Trim() ?? string.Empty)
                .ToArray();
            if (fields.Length == 0)
            {
                messages.Add(message(
                    "isearch.return-type.fields-missing",
                    $"iSearch return dataset '{group.Key}' must define at least one DefaultFields value."));
                continue;
            }

            if (fields.Any(string.IsNullOrWhiteSpace))
            {
                messages.Add(message(
                    "isearch.return-type.field-empty",
                    $"iSearch return dataset '{group.Key}' contains an empty DefaultFields value."));
                continue;
            }

            if (fields.Distinct(StringComparer.Ordinal).Count() != fields.Length)
            {
                messages.Add(message(
                    "isearch.return-type.field-duplicate",
                    $"iSearch return dataset '{group.Key}' contains duplicate DefaultFields values."));
                continue;
            }

            definitions.Add(new SearchReturnTypeDefinition
            {
                Name = group.Key,
                DefaultFields = Array.AsReadOnly(fields)
            });
        }

        return messages.Count == 0
            ? OperationResult<IReadOnlyList<SearchReturnTypeDefinition>>.Success(
                Array.AsReadOnly(definitions.ToArray()))
            : OperationResult<IReadOnlyList<SearchReturnTypeDefinition>>.Failure(messages);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one safe failed catalog result.</summary>
    /// <param name="code">The stable configuration failure code.</param>
    /// <param name="text">The operator-facing failure text.</param>
    /// <returns>A failed operation result without a usable catalog.</returns>
    private static OperationResult<IReadOnlyList<SearchReturnTypeDefinition>> failure(string code, string text)
    {
        #region implementation

        return OperationResult<IReadOnlyList<SearchReturnTypeDefinition>>.Failure([message(code, text)]);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one error message for return-dataset configuration.</summary>
    /// <param name="code">The stable message code.</param>
    /// <param name="text">The safe message text.</param>
    /// <returns>The configuration error message.</returns>
    private static OperationMessage message(string code, string text)
    {
        #region implementation

        return new OperationMessage
        {
            Code = code,
            Message = text,
            Severity = OperationMessageSeverity.Error
        };

        #endregion
    }

    #endregion
}
