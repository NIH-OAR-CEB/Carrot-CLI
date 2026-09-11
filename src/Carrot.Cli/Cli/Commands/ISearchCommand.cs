using Carrot.Cli.Cli.Settings;
using Carrot.Cli.Cli.UI;
using Carrot.Cli.Common;
using Carrot.Cli.ISearch;
using Carrot.Cli.Processing;
using Spectre.Console.Cli;

namespace Carrot.Cli.Cli.Commands;

/**************************************************************/
/// <summary>Runs the named prompt-free iSearch search and optional downstream operations.</summary>
/// <seealso cref="ISearchCommandWorkflow"/>
internal sealed class ISearchCommand : AsyncCommand<ISearchSettings>
{
    #region implementation

    private readonly ISearchCommandWorkflow _workflow;
    private readonly ConsoleReporter _reporter;

    /**************************************************************/
    /// <summary>Initializes the command with the named workflow and safe console reporter.</summary>
    /// <param name="workflow">The prompt-free iSearch workflow.</param>
    /// <param name="reporter">The literal diagnostic and summary reporter.</param>
    /// <exception cref="ArgumentNullException">Thrown when a dependency is null.</exception>
    public ISearchCommand(ISearchCommandWorkflow workflow, ConsoleReporter reporter)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(reporter);
        _workflow = workflow;
        _reporter = reporter;

        #endregion
    }

    /**************************************************************/
    /// <summary>Translates bound settings into one workflow request and returns its stable exit code.</summary>
    /// <param name="context">The Spectre command context.</param>
    /// <param name="settings">The parsed iSearch settings.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>The documented iSearch command exit code.</returns>
    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        ISearchSettings settings,
        CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            var result = await _workflow.RunAsync(new SearchCommandRequest
            {
                Database = settings.Database!.Trim(),
                ReturnDataset = settings.ResultDataset!.Trim(),
                Query = settings.Query,
                QueryFields = settings.QueryFields,
                FilterQueries = settings.FilterQueries,
                DefaultOp = settings.DefaultOp,
                Rows = settings.Rows,
                UpdatedAfter = settings.UpdatedAfter,
                UpdatedBefore = settings.UpdatedBefore,
                MaxResults = settings.MaxResults,
                AllResults = settings.AllResults,
                OutputPath = settings.OutputPath,
                Categorize = settings.Categorize,
                CategorizedOutputPath = settings.CategorizedOutputPath,
                Endpoint = settings.Endpoint,
                TimeoutSeconds = settings.TimeoutSeconds,
                Overwrite = settings.Overwrite
            }, cancellationToken).ConfigureAwait(false);

            if (result.Status == OperationStatus.Failure)
            {
                _reporter.WriteMessages(result.Messages);
                return classifyFailure(result.Messages);
            }

            _reporter.WriteISearchResult(result, settings.Quiet);
            return result.Status == OperationStatus.PartialSuccess
                ? ExitCodes.PartialSuccess
                : ExitCodes.Success;
        }
        catch (OperationCanceledException)
        {
            return ExitCodes.Cancellation;
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Maps iSearch and downstream diagnostics to the command's stable exit-code categories.</summary>
    /// <param name="messages">The workflow diagnostics.</param>
    /// <returns>The appropriate automation exit code.</returns>
    private static int classifyFailure(IReadOnlyList<OperationMessage> messages)
    {
        #region implementation

        var firstCode = messages.FirstOrDefault()?.Code ?? string.Empty;
        if (firstCode.Contains("report.write", StringComparison.Ordinal))
        {
            return ExitCodes.OutputFailure;
        }

        if (firstCode.StartsWith("isearch.categorization", StringComparison.Ordinal))
        {
            return ExitCodes.ClusterFailure;
        }

        if (firstCode.StartsWith("isearch.configuration", StringComparison.Ordinal)
            || firstCode.StartsWith("isearch.request", StringComparison.Ordinal)
            || firstCode.StartsWith("isearch.return-type", StringComparison.Ordinal)
            || firstCode.StartsWith("isearch.field", StringComparison.Ordinal)
            || firstCode.StartsWith("isearch.filter", StringComparison.Ordinal)
            || firstCode.StartsWith("isearch.output", StringComparison.Ordinal)
            || firstCode.StartsWith("report.path", StringComparison.Ordinal))
        {
            return ExitCodes.InvalidConfiguration;
        }

        if (firstCode.StartsWith("isearch.", StringComparison.Ordinal))
        {
            return ExitCodes.ISearchFailure;
        }

        if (firstCode.StartsWith("report.", StringComparison.Ordinal))
        {
            return ExitCodes.OutputFailure;
        }

        if (firstCode.StartsWith("endpoint.", StringComparison.Ordinal)
            || firstCode.StartsWith("timeout.", StringComparison.Ordinal))
        {
            return ExitCodes.InvalidConfiguration;
        }

        return ExitCodes.InvalidConfiguration;

        #endregion
    }

    #endregion
}
