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

    private readonly OperationResult<SearchReturnTypeConfiguration> _result;

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
    /// <summary>Gets the validated result configuration or safe configuration diagnostics.</summary>
    /// <returns>The shared cardinality names and ordered return definitions on success, or invalid-configuration messages.</returns>
    public OperationResult<SearchReturnTypeConfiguration> GetConfiguration()
    {
        #region implementation

        return _result;

        #endregion
    }

    /**************************************************************/
    /// <summary>Builds an immutable result configuration from the nested <c>Results</c> section.</summary>
    /// <param name="section">The <c>iSearchReturnTypes</c> section.</param>
    /// <returns>A validated result configuration or accumulated configuration failures.</returns>
    private static OperationResult<SearchReturnTypeConfiguration> load(IConfigurationSection section)
    {
        #region implementation

        var resultsSection = section.GetSection("Results");
        // Results is the contract boundary for this feature. Without it, neither the shared
        // cardinality names nor any return dataset can be interpreted safely.
        if (!resultsSection.Exists())
        {
            return failure("isearch.return-types.missing", "No iSearch return datasets are configured under iSearchReturnTypes.");
        }

        var cardinalitySection = resultsSection.GetSection("Cardinality");
        var cardinality = new SearchCardinalityFieldNames
        {
            // Trim operator-supplied labels at the configuration boundary so equivalent labels do
            // not differ only because of accidental surrounding whitespace.
            TotalResultsFieldName = cardinalitySection["TotalResultsFieldName"]?.Trim() ?? string.Empty,
            CurrentResultsFieldName = cardinalitySection["CurrentResultsFieldName"]?.Trim() ?? string.Empty,
            PageNumberFieldName = cardinalitySection["PageNumberFieldName"]?.Trim() ?? string.Empty,
            TotalPagesFieldName = cardinalitySection["TotalPagesFieldName"]?.Trim() ?? string.Empty
        };

        var cardinalityValues = new[]
        {
            cardinality.TotalResultsFieldName,
            cardinality.CurrentResultsFieldName,
            cardinality.PageNumberFieldName,
            cardinality.TotalPagesFieldName
        };

        // Validate the four names together so every consumer can rely on a complete nested model
        // and does not need to invent fallback labels at rendering time.
        if (cardinalityValues.Any(string.IsNullOrWhiteSpace))
        {
            return failure("isearch.cardinality.field-name-missing", "iSearch Results.Cardinality must define four nonempty field names.");
        }

        // Distinct names are required because duplicate labels would make the terminal report
        // ambiguous to a human and difficult for future automation to parse reliably.
        if (cardinalityValues.Distinct(StringComparer.Ordinal).Count() != cardinalityValues.Length)
        {
            return failure("isearch.cardinality.field-name-duplicate", "iSearch Results.Cardinality field names must be unique.");
        }

        // Cardinality is shared metadata, not a selectable return dataset, so remove it from the
        // dataset definitions while preserving the configuration order of all other groups.
        var groups = resultsSection.GetChildren()
            .Where(group => !string.Equals(group.Key, "Cardinality", StringComparison.Ordinal))
            .ToArray();

        // A successful catalog must contain at least one selectable return dataset.
        if (groups.Length == 0)
        {
            return failure("isearch.return-types.missing", "No iSearch return datasets are configured under iSearchReturnTypes:Results.");
        }

        var definitions = new List<SearchReturnTypeDefinition>(groups.Length);
        var messages = new List<OperationMessage>();

        // Validate every group and accumulate diagnostics so one configuration load can explain
        // all invalid datasets instead of forcing the operator through one correction at a time.
        foreach (var group in groups)
        {
            // An empty key cannot be selected or named in an actionable diagnostic.
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

            // A return dataset without fields cannot produce a meaningful fl request.
            if (fields.Length == 0)
            {
                messages.Add(message(
                    "isearch.return-type.fields-missing",
                    $"iSearch return dataset '{group.Key}' must define at least one DefaultFields value."));
                continue;
            }

            // Reject blank entries rather than allowing an empty field token into the encoded
            // request where it could change service-side field selection semantics.
            if (fields.Any(string.IsNullOrWhiteSpace))
            {
                messages.Add(message(
                    "isearch.return-type.field-empty",
                    $"iSearch return dataset '{group.Key}' contains an empty DefaultFields value."));
                continue;
            }

            // Duplicate fields add no information and make the configured request harder to audit.
            if (fields.Distinct(StringComparer.Ordinal).Count() != fields.Length)
            {
                messages.Add(message(
                    "isearch.return-type.field-duplicate",
                    $"iSearch return dataset '{group.Key}' contains duplicate DefaultFields values."));
                continue;
            }

            // Only fully valid groups become definitions; their original order is retained for a
            // predictable interactive picker and stable request behavior.
            definitions.Add(new SearchReturnTypeDefinition
            {
                Name = group.Key,
                // Preserve configured field order because it becomes the order of the service fl
                // parameter and therefore the order users see when reviewing their return set.
                DefaultFields = Array.AsReadOnly(fields)
            });
        }

        // Publish the catalog only when every group passed validation. Partial success would make
        // a menu appear usable while silently omitting configured datasets.
        return messages.Count == 0
            ? OperationResult<SearchReturnTypeConfiguration>.Success(new SearchReturnTypeConfiguration
            {
                Cardinality = cardinality,
                ReturnTypes = Array.AsReadOnly(definitions.ToArray())
            })
            : OperationResult<SearchReturnTypeConfiguration>.Failure(messages);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one safe failed catalog result.</summary>
    /// <param name="code">The stable configuration failure code.</param>
    /// <param name="text">The operator-facing failure text.</param>
    /// <returns>A failed operation result without a usable catalog.</returns>
    private static OperationResult<SearchReturnTypeConfiguration> failure(string code, string text)
    {
        #region implementation

        return OperationResult<SearchReturnTypeConfiguration>.Failure([message(code, text)]);

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
