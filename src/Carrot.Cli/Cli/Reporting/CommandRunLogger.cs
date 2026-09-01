using System.Globalization;
using System.Text;
using Carrot.Cli.Common;
using Carrot.Cli.Processing;

namespace Carrot.Cli.Cli.Reporting;

/**************************************************************/
/// <summary>
/// Appends deterministic, line-oriented diagnostics to explicit command log files.
/// </summary>
/// <remarks>
/// One singleton instance serializes writes from commands running in this process. Files are opened in append mode
/// without truncation and permit other processes to append, although cross-process record order is not guaranteed.
/// The logger never receives document text, HTTP payloads, credentials, or exception details.
/// </remarks>
/// <seealso cref="ICommandRunLogger"/>
/// <seealso cref="ProcessRunResult"/>
internal sealed class CommandRunLogger : ICommandRunLogger
{
    #region implementation

    private static readonly UTF8Encoding Utf8WithoutByteOrderMark = new(encoderShouldEmitUTF8Identifier: false);
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly TimeProvider _timeProvider;

    /**************************************************************/
    /// <summary>
    /// Initializes the logger with the clock used to timestamp invariant UTC records.
    /// </summary>
    /// <param name="timeProvider">The clock used for deterministic record timestamps.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="timeProvider"/> is null.</exception>
    public CommandRunLogger(TimeProvider timeProvider)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Appends a safe command-start record when the caller selected a log file.
    /// </summary>
    /// <param name="logFile">The normalized optional log-file path.</param>
    /// <param name="commandName">The stable command name being executed.</param>
    /// <param name="cancellationToken">The token that can cancel the write before it begins.</param>
    /// <returns>A task representing the asynchronous append operation.</returns>
    public Task WriteStartedAsync(string? logFile, string commandName, CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        return appendAsync(logFile, [$"Information [run.started] Command started: {singleLine(commandName)}."], cancellationToken);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Appends safe terminal diagnostics and artifact paths for one completed command run.
    /// </summary>
    /// <param name="logFile">The normalized optional log-file path.</param>
    /// <param name="result">The completed run result to record.</param>
    /// <param name="cancellationToken">The token that can cancel the write before it begins.</param>
    /// <returns>A task representing the asynchronous append operation.</returns>
    public Task WriteResultAsync(string? logFile, ProcessRunResult result, CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(result);

        var records = new List<string>(result.Messages.Count + result.ArtifactPaths.Count + 4);
        foreach (var message in result.Messages)
        {
            ArgumentNullException.ThrowIfNull(message);
            records.Add($"{severityLabel(message.Severity)} [{singleLine(message.Code)}] {singleLine(message.Message)}");
        }

        records.Add($"Information [run.completed] Run ID: {result.RunId:D}.");
        records.Add($"Information [run.status] Status: {result.Status}.");
        records.Add($"Information [run.exit-code] Exit code: {result.ExitCode.ToString(CultureInfo.InvariantCulture)}.");

        foreach (var artifactPath in result.ArtifactPaths)
        {
            ArgumentNullException.ThrowIfNull(artifactPath);
            records.Add($"Information [artifact.path] Path: {singleLine(artifactPath)}");
        }

        return appendAsync(logFile, records, cancellationToken);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Appends a cancellation record without allowing the already-canceled command token to suppress it.
    /// </summary>
    /// <param name="logFile">The normalized optional log-file path.</param>
    /// <returns>A task representing the asynchronous append operation.</returns>
    public Task WriteCancellationAsync(string? logFile)
    {
        #region implementation

        return appendAsync(logFile, ["Warning [run.cancelled] Command cancelled."], CancellationToken.None);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Serializes one logical record group and appends it as UTF-8 text without truncating existing content.
    /// </summary>
    /// <param name="logFile">The normalized optional log-file path.</param>
    /// <param name="records">The already-safe records to append.</param>
    /// <param name="cancellationToken">The token that can cancel the append before it begins.</param>
    /// <returns>A task representing the append operation.</returns>
    private async Task appendAsync(string? logFile, IReadOnlyList<string> records, CancellationToken cancellationToken)
    {
        #region implementation

        if (logFile is null)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var stream = new FileStream(
                logFile,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite,
                bufferSize: 4096,
                useAsync: true);
            await using var writer = new StreamWriter(stream, Utf8WithoutByteOrderMark, bufferSize: 4096, leaveOpen: false);

            foreach (var record in records)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await writer.WriteLineAsync($"{_timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture)} {record}").ConfigureAwait(false);
            }

            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Converts one supported severity to its stable text label.
    /// </summary>
    /// <param name="severity">The operation-message severity.</param>
    /// <returns>The stable diagnostic label.</returns>
    private static string severityLabel(OperationMessageSeverity severity)
    {
        #region implementation

        return severity switch
        {
            OperationMessageSeverity.Information => "Information",
            OperationMessageSeverity.Warning => "Warning",
            OperationMessageSeverity.Error => "Error",
            _ => throw new InvalidOperationException($"Unsupported message severity: {severity}")
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Converts line breaks and control characters to spaces so every diagnostic remains exactly one text line.
    /// </summary>
    /// <param name="value">The value supplied by a safe operation boundary.</param>
    /// <returns>A single-line diagnostic value.</returns>
    private static string singleLine(string value)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(value);
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsControl(character) ? ' ' : character);
        }

        return builder.ToString();

        #endregion
    }

    #endregion
}
