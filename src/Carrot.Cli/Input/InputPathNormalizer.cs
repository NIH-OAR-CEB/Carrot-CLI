using Carrot.Cli.Common;

namespace Carrot.Cli.Input;

/**************************************************************/
/// <summary>
/// Normalizes interactive file-system paths while accepting matching surrounding quotes.
/// </summary>
/// <remarks>
/// One prompt value represents one path. Shell-style wildcard expansion, environment-variable
/// substitution, and multiple whitespace-separated paths are intentionally not performed.
/// </remarks>
internal sealed class InputPathNormalizer
{
    #region implementation

    /**************************************************************/
    /// <summary>Normalizes one quoted or unquoted path to an absolute path.</summary>
    /// <param name="input">The raw prompt value.</param>
    /// <returns>A normalized absolute path or a structured validation failure.</returns>
    internal OperationResult<string> Normalize(string? input)
    {
        #region implementation

        var value = input?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return failure("input.path.empty", "Enter a file, folder, or ZIP path.");
        }

        var beginsWithQuote = value[0] is '\'' or '"';
        var endsWithQuote = value[^1] is '\'' or '"';
        if (beginsWithQuote || endsWithQuote)
        {
            if (!beginsWithQuote || !endsWithQuote || value.Length < 2 || value[0] != value[^1])
            {
                return failure("input.path.quotes", "Quoted paths must use matching surrounding quotes.");
            }

            value = value[1..^1].Trim();
            if (value.Length == 0)
            {
                return failure("input.path.empty", "The quoted path must not be empty.");
            }
        }

        try
        {
            return OperationResult<string>.Success(Path.GetFullPath(value));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return failure("input.path.invalid", $"The path is invalid: {exception.Message}");
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a failed normalization result with one coded error.</summary>
    /// <param name="code">The stable diagnostic code.</param>
    /// <param name="message">The user-facing diagnostic.</param>
    /// <returns>A failed path result.</returns>
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
