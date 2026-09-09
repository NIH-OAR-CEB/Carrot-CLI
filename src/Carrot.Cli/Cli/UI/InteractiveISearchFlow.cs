using System.Text.Json;
using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Carrot.Cli.ISearch;
using Carrot.Cli.ISearch.Contracts;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>Coordinates the prompt-driven iSearch health, dataset, and query experience.</summary>
/// <remarks>
/// The flow performs no network work before local configuration validation and never persists the
/// selected dataset, query, or returned records. The parent menu remains responsible for cancellation.
/// </remarks>
/// <seealso cref="ISearchFlow"/>
/// <seealso cref="IISearchApiClient"/>
/// <seealso cref="ISearchResultsPager"/>
/// <seealso cref="ISearchFieldsPager"/>
internal sealed class InteractiveISearchFlow : ISearchFlow
{
    #region implementation

    private readonly IAnsiConsole _console;
    private readonly ISearchOptions _options;
    private readonly ISearchOptionsValidator _optionsValidator;
    private readonly SearchReturnTypeCatalog _returnTypeCatalog;
    private readonly IISearchApiClient _client;
    private readonly ISearchResultsPager _resultsPager;
    private readonly ISearchFieldsPager _fieldsPager;

    /**************************************************************/
    /// <summary>Initializes the interactive flow with configuration, API, and presentation boundaries.</summary>
    /// <param name="console">The interactive console.</param>
    /// <param name="options">The optional iSearch configuration.</param>
    /// <param name="optionsValidator">The feature-local configuration validator.</param>
    /// <param name="returnTypeCatalog">The validated configured return-dataset catalog.</param>
    /// <param name="client">The authenticated iSearch API boundary.</param>
    /// <param name="resultsPager">The bounded generic-record renderer.</param>
    /// <param name="fieldsPager">The bounded field-metadata renderer.</param>
    public InteractiveISearchFlow(
        IAnsiConsole console,
        IOptions<ISearchOptions> options,
        ISearchOptionsValidator optionsValidator,
        SearchReturnTypeCatalog returnTypeCatalog,
        IISearchApiClient client,
        ISearchResultsPager resultsPager,
        ISearchFieldsPager fieldsPager)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(optionsValidator);
        ArgumentNullException.ThrowIfNull(returnTypeCatalog);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(resultsPager);
        ArgumentNullException.ThrowIfNull(fieldsPager);
        _console = console;
        _options = options.Value;
        _optionsValidator = optionsValidator;
        _returnTypeCatalog = returnTypeCatalog;
        _client = client;
        _resultsPager = resultsPager;
        _fieldsPager = fieldsPager;

        #endregion
    }

    /**************************************************************/
    /// <summary>Checks availability, discovers live datasets, and serves the iSearch submenu.</summary>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task representing the iSearch visit.</returns>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        #region implementation

        var returnTypesResult = _returnTypeCatalog.GetConfiguration();
        // Configuration is evaluated before any service call so a malformed return-dataset
        // definition is reported as a local, actionable problem instead of becoming a remote request error.
        if (returnTypesResult.Status == OperationStatus.Failure)
        {
            writeMessages(returnTypesResult.Messages);
            return;
        }

        // Validate locally before health so an absent key cannot result in any authenticated request.
        var configurationResult = _optionsValidator.Validate(_options);
        // Credentials and safety limits are checked before health because no authenticated network
        // operation is useful when the local options cannot support a safe iSearch request.
        if (configurationResult.Status == OperationStatus.Failure)
        {
            writeMessages(configurationResult.Messages);
            return;
        }

        _console.MarkupLine("[orange1]Checking iSearch availability...[/]");
        var healthResult = await _client.GetHealthAsync(cancellationToken).ConfigureAwait(false);
        // A failed health operation already contains the user-facing diagnostic; stop this visit
        // here so the menu never presents choices that cannot currently work.
        if (healthResult.Status == OperationStatus.Failure)
        {
            writeMessages(healthResult.Messages);
            return;
        }

        _console.WriteLine("iSearch availability response:");
        _console.WriteLine(JsonSerializer.Serialize(healthResult.Value!.Payload, new JsonSerializerOptions { WriteIndented = true }));

        // Dataset choices are meaningful only after an explicitly positive service-health response.
        // Treat every other status, including a missing status value, as unavailable rather than
        // guessing that discovery is safe to attempt.
        if (!string.Equals(healthResult.Value.Status, "UP", StringComparison.OrdinalIgnoreCase))
        {
            _console.MarkupLine("[yellow]iSearch is not available; dataset discovery was not attempted.[/]");
            return;
        }

        _console.MarkupLine("[orange1]Discovering iSearch datasets...[/]");
        var datasetsResult = await _client.GetDatasetsAsync(cancellationToken).ConfigureAwait(false);
        // Discovery errors are terminal for this visit because the interactive menu must use the
        // exact live dataset names returned by iSearch.
        if (datasetsResult.Status == OperationStatus.Failure)
        {
            writeMessages(datasetsResult.Messages);
            return;
        }

        // An empty successful response is different from a transport failure, but it still leaves
        // the user with no valid dataset to select, so there is nothing useful to prompt for.
        if (datasetsResult.Value!.Count == 0)
        {
            _console.WriteLine("iSearch returned no datasets.");
            return;
        }

        await runDatasetMenuAsync(
            datasetsResult.Value,
            returnTypesResult.Value!,
            cancellationToken).ConfigureAwait(false);

        #endregion
    }

    /**************************************************************/
    /// <summary>Retains the selected database and return dataset while offering iSearch actions.</summary>
    /// <param name="datasets">The exact live dataset names returned by iSearch.</param>
    /// <param name="configuration">The validated return types and shared cardinality report names.</param>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    private async Task runDatasetMenuAsync(
        IReadOnlyList<string> datasets,
        SearchReturnTypeConfiguration configuration,
        CancellationToken cancellationToken)
    {
        #region implementation

        string? selectedDatabase = null;
        SearchReturnTypeDefinition? selectedReturnType = null;
        // The loop represents one complete menu visit. Each action returns here when it is done,
        // allowing the user to reuse the current database and return type without restarting the flow.
        while (true)
        {
            // Keep both selections in this visit so returning from a query does not force reselection.
            var choices = new List<DatasetChoice> { DatasetChoice.SelectDatabase };
            // Return-dataset, field, and query actions are database-scoped. Hiding them until a
            // database exists prevents invalid states from reaching the client.
            if (selectedDatabase is not null)
            {
                choices.Add(DatasetChoice.SelectReturnDataset);
                choices.Add(DatasetChoice.ViewFields);
                // A query is enabled only after both parts of its request context have been chosen.
                if (selectedReturnType is not null)
                {
                    choices.Add(DatasetChoice.SubmitQuery);
                }
            }

            choices.Add(DatasetChoice.Back);
            // The title communicates whether the next choice establishes context or operates
            // within the database already retained by this menu visit.
            var selected = await new SelectionPrompt<DatasetChoice>()
                .Title(selectedDatabase is null
                    ? "[bold orange1]iSearch[/] — Select a dataset"
                    : $"[bold orange1]iSearch[/] — Database: {Markup.Escape(selectedDatabase)}")
                .HighlightStyle(new Style(Color.Black, Color.Orange1))
                .UseConverter(choice => choice switch
                {
                    // Keep display labels human-readable while retaining enum values for dispatch.
                    DatasetChoice.SelectDatabase => "Select Database",
                    DatasetChoice.SelectReturnDataset => "Select Return Dataset",
                    DatasetChoice.ViewFields => "View Fields",
                    DatasetChoice.SubmitQuery => "Submit Query",
                    DatasetChoice.Back => "Back to Main Menu",
                    _ => choice.ToString()
                })
                .AddChoices(choices)
                .AddCancelResult(DatasetChoice.Back)
                .ShowAsync(_console, cancellationToken)
                .ConfigureAwait(false);

            switch (selected)
            {
                case DatasetChoice.SelectDatabase:
                    // Changing databases invalidates the prior return-type selection because that
                    // selection is configuration for the request, not a property of the service database.
                    var previousDatabase = selectedDatabase;
                    selectedDatabase = await selectDatabaseAsync(
                        datasets,
                        selectedDatabase,
                        cancellationToken).ConfigureAwait(false);
                    if (!string.Equals(previousDatabase, selectedDatabase, StringComparison.Ordinal))
                    {
                        selectedReturnType = null;
                    }

                    break;
                case DatasetChoice.SelectReturnDataset:
                    // The menu construction above normally makes this guard redundant. Keeping it
                    // here makes the dispatch safe if the choices are changed independently later.
                    if (selectedDatabase is not null)
                    {
                        selectedReturnType = await selectReturnDatasetAsync(
                            configuration.ReturnTypes,
                            selectedReturnType,
                            cancellationToken).ConfigureAwait(false);
                    }

                    break;
                case DatasetChoice.ViewFields:
                    // Field discovery requires the exact live database selected by the operator.
                    if (selectedDatabase is not null)
                    {
                        await viewFieldsAsync(selectedDatabase, cancellationToken).ConfigureAwait(false);
                    }

                    break;
                case DatasetChoice.SubmitQuery:
                    // Submission is valid only with a database and configured result fields. This
                    // guard protects the API boundary even if a new caller supplies this action.
                    if (selectedDatabase is not null && selectedReturnType is not null)
                    {
                        await submitQueryAsync(
                            selectedDatabase,
                            selectedReturnType,
                            configuration.Cardinality,
                            cancellationToken).ConfigureAwait(false);
                    }

                    break;
                case DatasetChoice.Back:
                    // Back is the explicit and Escape/cancel-generated exit from this nested menu.
                    return;
                default:
                    // An enum value not handled above indicates a programming/configuration defect,
                    // so silently returning would hide a broken menu contract.
                    throw new InvalidOperationException($"Unsupported iSearch action: {selected}");
            }
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Prompts from configured return datasets and preserves the prior choice on Escape.</summary>
    /// <param name="returnTypes">The validated return datasets loaded from configuration.</param>
    /// <param name="currentReturnType">The currently selected return dataset, if any.</param>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>The chosen return dataset, or the prior choice when Escape is pressed.</returns>
    private async Task<SearchReturnTypeDefinition?> selectReturnDatasetAsync(
        IReadOnlyList<SearchReturnTypeDefinition> returnTypes,
        SearchReturnTypeDefinition? currentReturnType,
        CancellationToken cancellationToken)
    {
        #region implementation

        var selected = await new SelectionPrompt<string>()
            .Title("[bold orange1]iSearch Return Datasets[/]")
            .HighlightStyle(new Style(Color.Black, Color.Orange1))
            .AddChoices(returnTypes.Select(returnType => returnType.Name))
            .AddCancelResult(string.Empty)
            .ShowAsync(_console, cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrEmpty(selected))
        {
            // Escape cancels only this picker. Preserve the existing selection so cancellation does
            // not unexpectedly erase valid request context.
            return currentReturnType;
        }

        // The picker returns a configured name; resolve it back to the full definition so the query
        // receives its complete, validated field list rather than only the display text.
        // The catalog is already validated, so an exact ordinal lookup maps the visible name to
        // one and only one immutable definition.
        var selectedReturnType = returnTypes.Single(returnType =>
            string.Equals(returnType.Name, selected, StringComparison.Ordinal));
        _console.WriteLine($"Selected iSearch return dataset: {selectedReturnType.Name}");
        return selectedReturnType;

        #endregion
    }

    /**************************************************************/
    /// <summary>Retrieves and displays fields for the retained dataset before returning to its menu.</summary>
    /// <param name="database">The exact selected dataset name.</param>
    /// <param name="cancellationToken">The token signaling request or console cancellation.</param>
    private async Task viewFieldsAsync(string database, CancellationToken cancellationToken)
    {
        #region implementation

        var result = await _client.GetFieldsAsync(database, cancellationToken).ConfigureAwait(false);
        // Field retrieval failures are displayed at this level and then return the user to the
        // retained database menu; they do not invalidate the database selection itself.
        if (result.Status == OperationStatus.Failure)
        {
            writeMessages(result.Messages);
            return;
        }

        await _fieldsPager.ShowAsync(result.Value!, cancellationToken).ConfigureAwait(false);

        #endregion
    }

    /**************************************************************/
    /// <summary>Prompts from the exact live dataset choices and returns on Escape.</summary>
    /// <param name="datasets">The dataset names supplied by iSearch.</param>
    /// <param name="currentDatabase">The currently retained database, if one has been selected.</param>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>The selected database, or the prior selection when Escape is pressed.</returns>
    private async Task<string?> selectDatabaseAsync(
        IReadOnlyList<string> datasets,
        string? currentDatabase,
        CancellationToken cancellationToken)
    {
        #region implementation

        // Use the service-owned names verbatim; the client deliberately does not invent or normalize datasets.
        var selected = await new SelectionPrompt<string>()
            .Title("[bold orange1]iSearch Datasets[/]")
            .HighlightStyle(new Style(Color.Black, Color.Orange1))
            .AddChoices(datasets)
            .AddCancelResult(string.Empty)
            .ShowAsync(_console, cancellationToken)
            .ConfigureAwait(false);
        // Escape cancels only database replacement. Returning the existing name keeps the rest of
        // the menu state coherent and avoids forcing a new selection after a harmless cancellation.
        return string.IsNullOrEmpty(selected) ? currentDatabase : selected;

        #endregion
    }

    /**************************************************************/
    /// <summary>Validates and submits one query for the retained database and return dataset.</summary>
    /// <param name="database">The exact selected dataset name.</param>
    /// <param name="returnType">The configured return dataset whose fields will be requested.</param>
    /// <param name="cardinalityFieldNames">The configured labels for common result-cardinality values.</param>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    private async Task submitQueryAsync(
        string database,
        SearchReturnTypeDefinition returnType,
        SearchCardinalityFieldNames cardinalityFieldNames,
        CancellationToken cancellationToken)
    {
        #region implementation

        // Validate at the prompt so an empty query never reaches the client or consumes a request
        // slot; the user can correct it immediately in the same interaction.
        var query = await new TextPrompt<string>("Query [grey](free text or Lucene syntax)[/]:")
            .PromptStyle("yellow")
            .Validate(value => string.IsNullOrWhiteSpace(value)
                ? ValidationResult.Error("Query must not be empty.")
                : ValidationResult.Success())
            .ShowAsync(_console, cancellationToken)
            .ConfigureAwait(false);

        // Keep one stable query context so every service-page fetch reuses the selected database,
        // return fields, operator, and row limit instead of reconstructing them from UI state.
        var request = new SearchRequest
        {
            Dataset = database,
            Query = query.Trim(),
            Fields = returnType.DefaultFields,
            DefaultOp = "AND",
            Rows = 100
        };
        var result = await _client.SearchAsync(request, cancellationToken).ConfigureAwait(false);

        // Search failures are rendered as operation messages and return to the menu, allowing the
        // operator to adjust the query without losing the selected database or return dataset.
        if (result.Status == OperationStatus.Failure)
        {
            writeMessages(result.Messages);
            return;
        }

        // The session keeps the selected return dataset outside the pager while providing the
        // reusable one-step continuation contract needed by interactive navigation and future crawling.
        var session = new SearchResultPageSession(_client, request, result.Value!, returnType.Name);
        await _resultsPager.ShowAsync(session, cardinalityFieldNames, cancellationToken).ConfigureAwait(false);

        #endregion
    }

    /**************************************************************/
    /// <summary>Writes safe operation messages as literal terminal lines.</summary>
    /// <param name="messages">The messages returned by an iSearch operation.</param>
    private void writeMessages(IReadOnlyList<OperationMessage> messages)
    {
        #region implementation

        foreach (var message in messages)
        {
            // Write messages as literal lines so service-provided text cannot be interpreted as
            // Spectre markup while the user is diagnosing a failed operation.
            _console.WriteLine($"iSearch: {message.Message}");
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Defines iSearch dataset-menu actions.</summary>
    private enum DatasetChoice
    {
        /**************************************************************/
        /// <summary>Opens the live dataset picker.</summary>
        SelectDatabase,

        /**************************************************************/
        /// <summary>Opens the configured return-dataset picker.</summary>
        SelectReturnDataset,

        /**************************************************************/
        /// <summary>Displays field metadata for the retained dataset.</summary>
        ViewFields,

        /**************************************************************/
        /// <summary>Prompts for and submits a query for the retained dataset.</summary>
        SubmitQuery,

        /**************************************************************/
        /// <summary>Returns to the main menu.</summary>
        Back
    }

    #endregion
}
