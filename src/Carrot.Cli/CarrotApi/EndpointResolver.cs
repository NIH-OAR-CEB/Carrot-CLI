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
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    internal OperationResult<Uri> Resolve(string? endpoint)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
