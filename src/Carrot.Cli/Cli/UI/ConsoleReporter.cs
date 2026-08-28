using Carrot.Cli.CarrotApi.Contracts;
using Carrot.Cli.Processing;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>
/// Presents progress, errors, run summaries, and server configuration to the console.
/// </summary>
internal sealed class ConsoleReporter
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Displays the terminal summary for a processing or preview run.
    /// </summary>
    /// <param name="result">The completed run result.</param>
    /// <param name="quiet">Whether normal informational output is suppressed.</param>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    internal void WriteRunResult(ProcessRunResult result, bool quiet)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Displays algorithms, languages, and templates returned by the list endpoint.
    /// </summary>
    /// <param name="configuration">The exact configuration response contract.</param>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    internal void WriteServerInfo(ListResponse configuration)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
