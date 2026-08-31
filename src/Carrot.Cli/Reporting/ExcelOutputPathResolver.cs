using Carrot.Cli.Common;

namespace Carrot.Cli.Reporting;

/**************************************************************/
/// <summary>
/// Normalizes and validates an operator-supplied Excel workbook destination.
/// </summary>
/// <remarks>
/// Output validation differs from input validation because the final file is normally absent.
/// The parent directory must already exist; this component never creates directories.
/// </remarks>
internal sealed class ExcelOutputPathResolver
{
    #region implementation

    /**************************************************************/
    /// <summary>Resolves one quoted or unquoted workbook path to an absolute `.xlsx` destination.</summary>
    /// <param name="input">The raw path entered by the operator.</param>
    /// <returns>The normalized path or a structured expected validation failure.</returns>
    internal OperationResult<string> Resolve(string? input)
    {
        #region implementation

        var value = input?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return failure("report.path.empty", "Enter an Excel output path ending in .xlsx.");
        }

        var beginsWithQuote = value[0] is '\'' or '"';
        var endsWithQuote = value[^1] is '\'' or '"';
        if (beginsWithQuote || endsWithQuote)
        {
            if (!beginsWithQuote || !endsWithQuote || value.Length < 2 || value[0] != value[^1])
            {
                return failure("report.path.quotes", "Quoted paths must use matching surrounding quotes.");
            }

            value = value[1..^1].Trim();
            if (value.Length == 0)
            {
                return failure("report.path.empty", "The quoted Excel output path must not be empty.");
            }
        }

        try
        {
            var fullPath = Path.GetFullPath(value);
            if (!string.Equals(Path.GetExtension(fullPath), ".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                return failure("report.path.extension", "The Excel output path must end in .xlsx.");
            }

            if (Directory.Exists(fullPath))
            {
                return failure("report.path.directory", "The Excel output path must name a file, not a directory.");
            }

            var parentDirectory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(parentDirectory) || !Directory.Exists(parentDirectory))
            {
                return failure("report.path.parent", "The Excel output directory does not exist.");
            }

            return OperationResult<string>.Success(fullPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return failure("report.path.invalid", $"The Excel output path is invalid: {exception.Message}");
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one failed path-validation result.</summary>
    /// <param name="code">The stable report-path message code.</param>
    /// <param name="message">The safe operator-facing validation message.</param>
    /// <returns>A failed output-path result.</returns>
    private static OperationResult<string> failure(string code, string message)
    {
        #region implementation

        return OperationResult<string>.Failure(
        [
            new OperationMessage
            {
                Code = code,
                Message = message,
                Severity = OperationMessageSeverity.Error
            }
        ]);

        #endregion
    }

    #endregion
}
