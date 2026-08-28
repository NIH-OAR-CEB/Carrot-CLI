using Carrot.Cli.Cli.Settings;
using Carrot.Cli.Common;
using Carrot.Cli.Processing;

namespace Carrot.Cli.Configuration;

/**************************************************************/
/// <summary>
/// Resolves command settings, environment endpoint fallback, defaults, and validated run requests.
/// </summary>
/// <remarks>
/// Noninteractive endpoint precedence is the explicit option followed by
/// <c>CARROTCLI_ENDPOINT</c>. Missing endpoints produce expected configuration failures.
/// </remarks>
internal sealed class RunSettingsResolver
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Resolves complete processing settings into an immutable workflow request.
    /// </summary>
    /// <param name="settings">The command settings supplied by Spectre.</param>
    /// <returns>A successful request or structured configuration messages.</returns>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    internal OperationResult<ProcessRequest> ResolveProcess(ProcessSettings settings)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Resolves preview settings into an immutable workflow request.
    /// </summary>
    /// <param name="settings">The command settings supplied by Spectre.</param>
    /// <returns>A successful request or structured configuration messages.</returns>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    internal OperationResult<PreviewRequest> ResolvePreview(PreviewSettings settings)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Resolves endpoint settings using command-line then environment precedence.
    /// </summary>
    /// <param name="settings">The endpoint settings supplied by Spectre.</param>
    /// <returns>A successful absolute service URI or structured configuration messages.</returns>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    internal OperationResult<Uri> ResolveEndpoint(EndpointSettings settings)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
