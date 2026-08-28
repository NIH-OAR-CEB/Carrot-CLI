using Carrot.Cli.Common;
using Carrot.Cli.Input;

namespace Carrot.Cli.Extraction;

/**************************************************************/
/// <summary>
/// Creates consistently hashed extraction successes and coded expected file failures.
/// </summary>
/// <remarks>
/// Keeping result construction outside format-specific readers prevents their status, title,
/// hashing, and diagnostic behavior from drifting as new formats are added.
/// </remarks>
internal sealed class ExtractionResultFactory
{
    #region implementation

    private readonly HashService _hashService;

    /**************************************************************/
    /// <summary>Initializes extraction result creation with streamed source hashing.</summary>
    public ExtractionResultFactory(HashService hashService)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(hashService);
        _hashService = hashService;

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a successfully extracted document after validating content and hashing its source.</summary>
    internal async Task<ExtractionResult> CreateSuccessAsync(
        SourceFile sourceFile,
        string content,
        CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(sourceFile);
        if (string.IsNullOrWhiteSpace(content))
        {
            return CreateFailure(sourceFile, "extraction.empty", "The document contains no searchable text.");
        }

        try
        {
            await using var source = new FileStream(
                sourceFile.PhysicalPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);
            var sha256 = await _hashService.ComputeSha256Async(source, cancellationToken).ConfigureAwait(false);

            return new ExtractionResult
            {
                SourceFile = sourceFile,
                Outcome = OperationResult<ExtractedDocument>.Success(new ExtractedDocument
                {
                    SourceFile = sourceFile,
                    CarrotDocumentIndex = -1,
                    Title = Path.GetFileNameWithoutExtension(sourceFile.FileName),
                    Content = content,
                    Sha256 = sha256
                })
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return CreateFailure(sourceFile, "extraction.hash", $"Unable to hash the source file: {exception.Message}");
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one expected file-level extraction failure.</summary>
    internal ExtractionResult CreateFailure(SourceFile sourceFile, string code, string message)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(sourceFile);
        return new ExtractionResult
        {
            SourceFile = sourceFile,
            Outcome = OperationResult<ExtractedDocument>.Failure(
            [
                new OperationMessage
                {
                    Code = code,
                    Message = message,
                    Severity = OperationMessageSeverity.Error
                }
            ])
        };

        #endregion
    }

    #endregion
}
