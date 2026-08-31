using Microsoft.Extensions.Options;

namespace Carrot.Cli.Configuration;

/**************************************************************/
/// <summary>
/// Validates configured safety limits and defaults before the host accepts commands.
/// </summary>
/// <seealso cref="CarrotCliOptions"/>
internal sealed class CarrotCliOptionsValidator : IValidateOptions<CarrotCliOptions>
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Validates one named or default options instance and accumulates all configuration failures.
    /// </summary>
    /// <param name="name">The optional named-options key.</param>
    /// <param name="options">The bound options instance.</param>
    /// <returns>A success result or all validation errors.</returns>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    public ValidateOptionsResult Validate(string? name, CarrotCliOptions options)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        validatePositive(options.MaximumInputFiles, nameof(options.MaximumInputFiles), failures);
        validatePositive(options.MaximumFileSizeBytes, nameof(options.MaximumFileSizeBytes), failures);
        validatePositive(options.MaximumExpandedArchiveBytes, nameof(options.MaximumExpandedArchiveBytes), failures);
        validatePositive(options.MaximumTotalExtractedCharacters, nameof(options.MaximumTotalExtractedCharacters), failures);
        validatePositive(options.MaximumCompressionRatio, nameof(options.MaximumCompressionRatio), failures);
        validatePositive(options.ExtractionWorkerCount, nameof(options.ExtractionWorkerCount), failures);
        validatePositive(options.HttpTimeoutSeconds, nameof(options.HttpTimeoutSeconds), failures);
        validateNonnegative(options.TransientRetryCount, nameof(options.TransientRetryCount), failures);
        validatePositive(options.ContentPreviewCharacterLimit, nameof(options.ContentPreviewCharacterLimit), failures);
        if (options.ContentPreviewCharacterLimit > 32_767)
        {
            failures.Add($"{nameof(options.ContentPreviewCharacterLimit)} must not exceed Excel's 32,767-character cell limit.");
        }

        validatePositive(options.PreparedResultsPageSize, nameof(options.PreparedResultsPageSize), failures);
        validatePositive(options.ConsolePreviewCharacterLimit, nameof(options.ConsolePreviewCharacterLimit), failures);

        if (string.IsNullOrWhiteSpace(options.DefaultAlgorithm))
        {
            failures.Add($"{nameof(options.DefaultAlgorithm)} must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(options.DefaultLanguage))
        {
            failures.Add($"{nameof(options.DefaultLanguage)} must not be empty.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);

        #endregion
    }

    /**************************************************************/
    /// <summary>Validates that an integral configuration value is greater than zero.</summary>
    /// <param name="value">The configured value.</param>
    /// <param name="propertyName">The configuration property name.</param>
    /// <param name="failures">The collection receiving validation failures.</param>
    private static void validatePositive(long value, string propertyName, ICollection<string> failures)
    {
        #region implementation

        if (value <= 0)
        {
            failures.Add($"{propertyName} must be greater than zero.");
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Validates that a floating-point configuration value is finite and greater than zero.</summary>
    /// <param name="value">The configured value.</param>
    /// <param name="propertyName">The configuration property name.</param>
    /// <param name="failures">The collection receiving validation failures.</param>
    private static void validatePositive(double value, string propertyName, ICollection<string> failures)
    {
        #region implementation

        if (!double.IsFinite(value) || value <= 0D)
        {
            failures.Add($"{propertyName} must be finite and greater than zero.");
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Validates that an integral configuration value is zero or greater.</summary>
    /// <param name="value">The configured value.</param>
    /// <param name="propertyName">The configuration property name.</param>
    /// <param name="failures">The collection receiving validation failures.</param>
    private static void validateNonnegative(long value, string propertyName, ICollection<string> failures)
    {
        #region implementation

        if (value < 0)
        {
            failures.Add($"{propertyName} must not be negative.");
        }

        #endregion
    }

    #endregion
}
