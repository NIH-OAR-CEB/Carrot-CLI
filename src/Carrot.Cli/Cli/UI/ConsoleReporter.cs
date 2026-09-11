using System.Globalization;
using Carrot.Cli.CarrotApi.Contracts;
using Carrot.Cli.Common;
using Carrot.Cli.ISearch;
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
    /// <summary>Displays a safe summary for a named iSearch command result.</summary>
    /// <param name="result">The complete or partial iSearch operation result.</param>
    /// <param name="quiet">Whether normal informational output is suppressed.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="result"/> is null.</exception>
    /// <seealso cref="SearchCommandResult"/>
    internal void WriteISearchResult(
        OperationResult<SearchCommandResult> result,
        bool quiet)
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

        if (quiet || result.Value is not { } value)
        {
            return;
        }

        _console.Write(new Text($"iSearch database: {value.Database}{Environment.NewLine}"));
        _console.Write(new Text($"Return dataset: {value.ReturnDataset}{Environment.NewLine}"));
        _console.Write(new Text($"Loaded records: {value.Session.WalkedResults.Count.ToString(CultureInfo.InvariantCulture)}{Environment.NewLine}"));
        _console.Write(new Text($"Total records: {value.Session.WalkProgress.TotalRecords.ToString(CultureInfo.InvariantCulture)}{Environment.NewLine}"));
        _console.Write(new Text($"Complete: {value.IsComplete}{Environment.NewLine}"));
        _console.Write(new Text($"Categorized: {value.WasCategorized}{Environment.NewLine}"));
        _console.Write(new Text("Artifacts:" + Environment.NewLine));
        if (value.ArtifactPaths.Count == 0)
        {
            _console.Write(new Text("  (none)" + Environment.NewLine));
            return;
        }

        foreach (var path in value.ArtifactPaths)
        {
            _console.Write(new Text($"  {path}{Environment.NewLine}"));
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

        WriteServerInfoLines(CreateServerInfoLines(configuration));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Writes preformatted server-information lines as literal console text.
    /// </summary>
    /// <param name="lines">The ordered server-information lines to display.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lines"/> is null.</exception>
    /// <seealso cref="ListResponse"/>
    internal void WriteServerInfoLines(IReadOnlyList<string> lines)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(lines);
        foreach (var line in lines)
        {
            _console.Write(new Text($"{line}{Environment.NewLine}"));
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Creates deterministic literal lines for a server-information response.
    /// </summary>
    /// <param name="configuration">The exact configuration response contract.</param>
    /// <returns>Ordered headings and identifiers suitable for unpaged or paged output.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configuration"/> is null.</exception>
    /// <seealso cref="ListResponse"/>
    internal static IReadOnlyList<string> CreateServerInfoLines(ListResponse configuration)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(configuration);
        var lines = new List<string> { "Algorithms and languages:", string.Empty };
        if (configuration.Algorithms.Count == 0)
        {
            lines.Add("  (none)");
        }
        else
        {
            lines.AddRange(configuration.Algorithms
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(algorithm =>
                {
                    var languages = algorithm.Value.Count == 0
                        ? "(none)"
                        : string.Join(", ", algorithm.Value.OrderBy(value => value, StringComparer.Ordinal));
                    return $"  {algorithm.Key}: {languages}";
                }));
        }

        lines.Add(string.Empty);
        lines.Add("Templates:");
        lines.Add(string.Empty);
        if (configuration.Templates.Count == 0)
        {
            lines.Add("  (none)");
        }
        else
        {
            lines.AddRange(configuration.Templates.Keys
                .OrderBy(value => value, StringComparer.Ordinal)
                .Select(template => $"  {template}"));
        }

        return lines;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Writes a collection of coded diagnostics in their supplied order.
    /// </summary>
    /// <param name="messages">The safe operation messages to display.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="messages"/> is null.</exception>
    /// <seealso cref="OperationMessage"/>
    internal void WriteMessages(IReadOnlyList<OperationMessage> messages)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(messages);
        foreach (var message in messages)
        {
            writeMessage(message);
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
