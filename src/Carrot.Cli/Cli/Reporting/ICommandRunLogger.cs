using Carrot.Cli.Processing;

namespace Carrot.Cli.Cli.Reporting;

/**************************************************************/
/// <summary>
/// Writes safe, operator-selected diagnostics for one deferred named-command run.
/// </summary>
/// <remarks>
/// This boundary intentionally accepts an optional path so callers do not need conditional logging branches.
/// It records only lifecycle metadata, coded operation messages, terminal status, exit code, and artifact paths;
/// callers must not supply extracted content, request/response payloads, credentials, or exception details.
/// </remarks>
/// <seealso cref="CommandRunLogger"/>
/// <seealso cref="ProcessRunResult"/>
internal interface ICommandRunLogger
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Appends the start of a named command run when an operator selected a log file.
    /// </summary>
    /// <param name="logFile">The normalized optional log-file path.</param>
    /// <param name="commandName">The stable command name being executed.</param>
    /// <param name="cancellationToken">The token that can cancel the write before it begins.</param>
    /// <returns>A task representing the asynchronous append operation.</returns>
    Task WriteStartedAsync(string? logFile, string commandName, CancellationToken cancellationToken);

    /**************************************************************/
    /// <summary>
    /// Appends safe terminal diagnostics and created artifact paths for a completed command run.
    /// </summary>
    /// <param name="logFile">The normalized optional log-file path.</param>
    /// <param name="result">The completed run result to record.</param>
    /// <param name="cancellationToken">The token that can cancel the write before it begins.</param>
    /// <returns>A task representing the asynchronous append operation.</returns>
    Task WriteResultAsync(string? logFile, ProcessRunResult result, CancellationToken cancellationToken);

    /**************************************************************/
    /// <summary>
    /// Appends a cancellation record after a command observes cancellation.
    /// </summary>
    /// <param name="logFile">The normalized optional log-file path.</param>
    /// <returns>A task representing the asynchronous append operation.</returns>
    /// <remarks>
    /// Cancellation logging deliberately completes with a non-cancelable token so an already-canceled command can
    /// leave an operator-visible terminal record.
    /// </remarks>
    Task WriteCancellationAsync(string? logFile);

    #endregion
}
