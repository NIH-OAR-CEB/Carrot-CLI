using System.Globalization;
using Carrot.Cli.CarrotApi.Contracts;
using Carrot.Cli.Common;
using Carrot.Cli.Processing;
using Spectre.Console;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>
/// Presents progress, errors, run summaries, and server configuration to the console.
/// </summary>
/// <remarks>
/// Dynamic server and filesystem values are emitted as literal text so bracket characters cannot
/// be interpreted as Spectre markup. Output order is stable for interactive and redirected consoles.
/// </remarks>
internal sealed class ConsoleReporter
{
    #region implementation

    private readonly IAnsiConsole _console;

    /**************************************************************/
    /// <summary>
    /// Initializes the reporter with the console receiving named-command output.
    /// </summary>
    /// <param name="console">The injectable Spectre console.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="console"/> is null.</exception>
    public ConsoleReporter(IAnsiConsole console)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(console);
        _console = console;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Displays the terminal summary for a processing or preview run.
    /// </summary>
    /// <remarks>
    /// Quiet mode removes informational messages, the run summary, and artifact paths. Warnings and
    /// errors are always retained so unattended failures remain visible on standard output streams.
    /// </remarks>
    /// <param name="result">The completed run result.</param>
    /// <param name="quiet">Whether normal informational output is suppressed.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="result"/> is null.</exception>
    internal void WriteRunResult(ProcessRunResult result, bool quiet)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(result);

        foreach (var message in result.Messages)
        {
            if (!quiet || message.Severity is OperationMessageSeverity.Warning or OperationMessageSeverity.Error)
            {
                writeMessage(message);
            }
        }

        if (quiet)
        {
            return;
        }

        _console.WriteLine($"Run ID: {result.RunId:D}");
        _console.WriteLine($"Status: {result.Status}");
        _console.WriteLine($"Exit code: {result.ExitCode.ToString(CultureInfo.InvariantCulture)}");
        _console.WriteLine($"Rows: {result.Rows.Count.ToString(CultureInfo.InvariantCulture)}");
        _console.WriteLine("Artifacts:");

        if (result.ArtifactPaths.Count == 0)
        {
            _console.WriteLine("  (none)");
            return;
        }

        foreach (var artifactPath in result.ArtifactPaths)
        {
            _console.Write(new Text($"  {artifactPath}{Environment.NewLine}"));
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Displays algorithms, languages, and templates returned by the list endpoint.
    /// </summary>
    /// <param name="configuration">The exact configuration response contract.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configuration"/> is null.</exception>
    internal void WriteServerInfo(ListResponse configuration)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(configuration);

        _console.WriteLine("Algorithms and languages:");
        if (configuration.Algorithms.Count == 0)
        {
            _console.WriteLine("  (none)");
        }
        else
        {
            foreach (var algorithm in configuration.Algorithms.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                var languages = algorithm.Value.Count == 0
                    ? "(none)"
                    : string.Join(", ", algorithm.Value.OrderBy(value => value, StringComparer.Ordinal));
                _console.Write(new Text($"  {algorithm.Key}: {languages}{Environment.NewLine}"));
            }
        }

        _console.WriteLine("Templates:");
        if (configuration.Templates.Count == 0)
        {
            _console.WriteLine("  (none)");
            return;
        }

        foreach (var template in configuration.Templates.Keys.OrderBy(value => value, StringComparer.Ordinal))
        {
            _console.Write(new Text($"  {template}{Environment.NewLine}"));
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Writes one coded diagnostic message with a stable severity label.
    /// </summary>
    /// <param name="message">The safe operation message.</param>
    private void writeMessage(OperationMessage message)
    {
        #region implementation

        var severity = message.Severity switch
        {
            OperationMessageSeverity.Information => "Information",
            OperationMessageSeverity.Warning => "Warning",
            OperationMessageSeverity.Error => "Error",
            _ => throw new InvalidOperationException($"Unsupported message severity: {message.Severity}")
        };

        _console.Write(new Text($"{severity} [{message.Code}]: {message.Message}{Environment.NewLine}"));

        #endregion
    }

    #endregion
}
