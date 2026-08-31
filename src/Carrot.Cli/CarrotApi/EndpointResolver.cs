using Carrot.Cli.Common;

namespace Carrot.Cli.CarrotApi;

/**************************************************************/
/// <summary>
/// Normalizes and validates a Carrot service base endpoint before API calls are constructed.
/// </summary>
internal sealed class EndpointResolver
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Resolves a user-supplied endpoint into an absolute HTTP or HTTPS service URI.
    /// </summary>
    /// <param name="endpoint">The candidate endpoint text.</param>
    /// <returns>A successful service URI or structured configuration errors.</returns>
    internal OperationResult<Uri> Resolve(string? endpoint)
    {
        #region implementation

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return failure("endpoint.empty", "Enter a Carrot service endpoint ending in /service.");
        }

        var candidate = endpoint.Trim();
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return failure("endpoint.invalid", "The endpoint must be an absolute HTTP or HTTPS URL.");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return failure("endpoint.credentials", "The endpoint must not contain user credentials.");
        }

        if (!string.IsNullOrEmpty(uri.Query))
        {
            return failure("endpoint.query", "The endpoint must not contain a query string.");
        }

        if (!string.IsNullOrEmpty(uri.Fragment))
        {
            return failure("endpoint.fragment", "The endpoint must not contain a fragment.");
        }

        var normalizedPath = uri.AbsolutePath.TrimEnd('/');
        if (!normalizedPath.EndsWith("/service", StringComparison.Ordinal))
        {
            return failure("endpoint.path", "The endpoint path must end in /service.");
        }

        return OperationResult<Uri>.Success(new Uri(uri.AbsoluteUri.TrimEnd('/'), UriKind.Absolute));

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one endpoint-validation failure with a stable diagnostic code.</summary>
    /// <param name="code">The machine-readable endpoint error code.</param>
    /// <param name="message">The human-readable validation guidance.</param>
    /// <returns>A failed endpoint result.</returns>
    private static OperationResult<Uri> failure(string code, string message)
    {
        #region implementation

        return OperationResult<Uri>.Failure(
            [new OperationMessage { Code = code, Message = message, Severity = OperationMessageSeverity.Error }]);

        #endregion
    }

    #endregion
}
