using Carrot.Cli.CarrotApi.Contracts;
using Carrot.Cli.Common;

namespace Carrot.Cli.Configuration;

/**************************************************************/
/// <summary>Validates resolved clustering identifiers against the exact metadata returned by Carrot.</summary>
/// <remarks>
/// Identifier comparisons are always ordinal and case-sensitive, independent of the comparer used
/// by a caller-provided dictionary. Template validation checks its advertised name only because the
/// server remains authoritative for the template's effective algorithm, language, and parameters.
/// </remarks>
/// <seealso cref="ClusteringConfiguration"/>
/// <seealso cref="ListResponse"/>
internal sealed class ClusteringConfigurationValidator
{
    #region implementation

    /**************************************************************/
    /// <summary>Validates one resolved direct or template selection against a Carrot list response.</summary>
    /// <param name="configuration">The resolved clustering configuration.</param>
    /// <param name="availableConfiguration">The exact metadata returned by <c>/list</c>.</param>
    /// <returns>The supplied configuration on success, or one structured selection failure.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="configuration"/> or <paramref name="availableConfiguration"/> is null.
    /// </exception>
    public OperationResult<ClusteringConfiguration> Validate(
        ClusteringConfiguration configuration,
        ListResponse availableConfiguration)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(availableConfiguration);

        if (configuration.Template is not null)
        {
            if (configuration.Algorithm is not null || configuration.Language is not null)
            {
                return failure(
                    "clustering.selection.ambiguous",
                    "A template cannot be combined with an explicit algorithm or language.");
            }

            return availableConfiguration.Templates.Keys.Contains(configuration.Template, StringComparer.Ordinal)
                ? OperationResult<ClusteringConfiguration>.Success(configuration)
                : failure(
                    "clustering.template.unavailable",
                    $"Carrot does not advertise the exact template identifier '{configuration.Template}'.");
        }

        if (configuration.Algorithm is null || configuration.Language is null)
        {
            return failure(
                "clustering.selection.incomplete",
                "A direct clustering selection requires both an algorithm and a language.");
        }

        var algorithm = availableConfiguration.Algorithms
            .FirstOrDefault(entry => string.Equals(entry.Key, configuration.Algorithm, StringComparison.Ordinal));
        if (algorithm.Key is null)
        {
            return failure(
                "clustering.algorithm.unavailable",
                $"Carrot does not advertise the exact algorithm identifier '{configuration.Algorithm}'.");
        }

        return algorithm.Value.Contains(configuration.Language, StringComparer.Ordinal)
            ? OperationResult<ClusteringConfiguration>.Success(configuration)
            : failure(
                "clustering.language.unavailable",
                $"Carrot does not advertise the exact language identifier '{configuration.Language}' for algorithm '{configuration.Algorithm}'.");

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one failed validation result.</summary>
    /// <param name="code">The stable machine-readable code.</param>
    /// <param name="message">The safe user-facing diagnostic.</param>
    /// <returns>A failed clustering-configuration result.</returns>
    private static OperationResult<ClusteringConfiguration> failure(string code, string message)
    {
        #region implementation

        return OperationResult<ClusteringConfiguration>.Failure(
            [new OperationMessage { Code = code, Message = message, Severity = OperationMessageSeverity.Error }]);

        #endregion
    }

    #endregion
}
