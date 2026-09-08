using Carrot.Cli.Common;

namespace Carrot.Cli.Configuration;

/**************************************************************/
/// <summary>Validates optional iSearch credentials only when the iSearch workflow is entered.</summary>
/// <remarks>
/// The validator reports all missing prerequisites together and never includes the configured API key
/// in its diagnostic output.
/// </remarks>
/// <seealso cref="ISearchOptions"/>
internal sealed class ISearchOptionsValidator
{
    #region implementation

    /**************************************************************/
    /// <summary>Checks the values required before an authenticated iSearch request may be created.</summary>
    /// <param name="options">The configured iSearch options.</param>
    /// <returns>A successful result when both required values are present; otherwise safe diagnostics.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <see langword="null"/>.</exception>
    public OperationResult<bool> Validate(ISearchOptions options)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(options);

        var messages = new List<OperationMessage>();
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            messages.Add(new OperationMessage
            {
                Code = "isearch.configuration.api-key-missing",
                Message = "iSearch API key is not configured. Set iSearch:apiKey in User Secrets.",
                Severity = OperationMessageSeverity.Error
            });
        }

        if (string.IsNullOrWhiteSpace(options.ContactEmail))
        {
            messages.Add(new OperationMessage
            {
                Code = "isearch.configuration.contact-email-missing",
                Message = "iSearch contact email is not configured. Set iSearch:contactEmail in User Secrets.",
                Severity = OperationMessageSeverity.Error
            });
        }

        return messages.Count == 0
            ? OperationResult<bool>.Success(true)
            : OperationResult<bool>.Failure(messages);

        #endregion
    }

    #endregion
}
