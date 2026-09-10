using Carrot.Cli.Common;
using Carrot.Cli.CarrotApi;
using Carrot.Cli.Configuration;
using Carrot.Cli.ISearch;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>Owns endpoint prompting and feedback for iSearch-to-Carrot categorization.</summary>
/// <remarks>
/// Field mapping and Carrot execution remain in injected services; this flow only coordinates
/// interactive input, timeout configuration, and safe operator feedback.
/// </remarks>
/// <seealso cref="ISearchResultsCategorizationFlow"/>
internal sealed class SearchResultsCategorizationFlow : ISearchResultsCategorizationFlow
{
    #region implementation

    private readonly IAnsiConsole _console;
    private readonly EndpointResolver _endpointResolver;
    private readonly ISearchResultsCategorizer _categorizer;
    private readonly TimeSpan _httpTimeout;

    /**************************************************************/
    /// <summary>Initializes the iSearch categorization prompt flow.</summary>
    /// <param name="console">The interactive console.</param>
    /// <param name="endpointResolver">The shared Carrot endpoint validator.</param>
    /// <param name="categorizer">The iSearch-to-Carrot categorization boundary.</param>
    /// <param name="options">The validated CLI timeout settings.</param>
    public SearchResultsCategorizationFlow(
        IAnsiConsole console,
        EndpointResolver endpointResolver,
        ISearchResultsCategorizer categorizer,
        IOptions<CarrotCliOptions> options)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(endpointResolver);
        ArgumentNullException.ThrowIfNull(categorizer);
        ArgumentNullException.ThrowIfNull(options);
        _console = console;
        _endpointResolver = endpointResolver;
        _categorizer = categorizer;
        _httpTimeout = TimeSpan.FromSeconds(options.Value.HttpTimeoutSeconds);

        #endregion
    }

    /**************************************************************/
    /// <summary>Prompts for an endpoint and categorizes the session's currently loaded records.</summary>
    /// <param name="session">The iSearch session containing retained result pages.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>The categorized batch or a structured expected failure.</returns>
    public async Task<OperationResult<CategorizedISearchResultBatch>> RunAsync(
        SearchResultPageSession session,
        CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(session);
        var endpoint = await CarrotEndpointPrompt.PromptAsync(
                _console,
                _endpointResolver,
                "Carrot service endpoint [grey](the four loaded iSearch fields will be sent)[/]:",
                cancellationToken)
            .ConfigureAwait(false);

        _console.MarkupLine("[orange1]Validating Carrot configuration and categorizing loaded iSearch results…[/]");
        var result = await _categorizer
            .CategorizeAsync(session, endpoint, _httpTimeout, cancellationToken)
            .ConfigureAwait(false);
        if (result.Value is null)
        {
            foreach (var message in result.Messages)
            {
                _console.MarkupLine($"[red]iSearch categorization failed:[/] {Markup.Escape(message.Message)}");
            }
        }

        return result;

        #endregion
    }

    #endregion
}
