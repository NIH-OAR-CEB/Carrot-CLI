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

        var returnTypesResult = _returnTypeCatalog.GetDefinitions();
        if (returnTypesResult.Status == OperationStatus.Failure)
        {
            writeMessages(returnTypesResult.Messages);
            return;
        }

        // Validate locally before health so an absent key cannot result in any authenticated request.
        var configurationResult = _optionsValidator.Validate(_options);
        if (configurationResult.Status == OperationStatus.Failure)
        {
            writeMessages(configurationResult.Messages);
            return;
        }

        _console.MarkupLine("[orange1]Checking iSearch availability...[/]");
        var healthResult = await _client.GetHealthAsync(cancellationToken).ConfigureAwait(false);
        if (healthResult.Status == OperationStatus.Failure)
        {
            writeMessages(healthResult.Messages);
            return;
        }

        _console.WriteLine("iSearch availability response:");
        _console.WriteLine(JsonSerializer.Serialize(healthResult.Value!.Payload, new JsonSerializerOptions { WriteIndented = true }));

        // Dataset choices are meaningful only after an explicitly positive service-health response.
        if (!string.Equals(healthResult.Value.Status, "UP", StringComparison.OrdinalIgnoreCase))
        {
            _console.MarkupLine("[yellow]iSearch is not available; dataset discovery was not attempted.[/]");
            return;
        }

        _console.MarkupLine("[orange1]Discovering iSearch datasets...[/]");
        var datasetsResult = await _client.GetDatasetsAsync(cancellationToken).ConfigureAwait(false);
        if (datasetsResult.Status == OperationStatus.Failure)
        {
            writeMessages(datasetsResult.Messages);
            return;
        }

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
    /// <param name="returnTypes">The validated configured return datasets.</param>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    private async Task runDatasetMenuAsync(
        IReadOnlyList<string> datasets,
        IReadOnlyList<SearchReturnTypeDefinition> returnTypes,
        CancellationToken cancellationToken)
    {
        #region implementation

        string? selectedDatabase = null;
        SearchReturnTypeDefinition? selectedReturnType = null;
        while (true)
        {
            // Keep both selections in this visit so returning from a query does not force reselection.
            var choices = new List<DatasetChoice> { DatasetChoice.SelectDatabase };
            if (selectedDatabase is not null)
            {
                choices.Add(DatasetChoice.SelectReturnDataset);
                choices.Add(DatasetChoice.ViewFields);
                if (selectedReturnType is not null)
                {
                    choices.Add(DatasetChoice.SubmitQuery);
                }
            }

            choices.Add(DatasetChoice.Back);
            var selected = await new SelectionPrompt<DatasetChoice>()
                .Title(selectedDatabase is null
                    ? "[bold orange1]iSearch[/] — Select a dataset"
                    : $"[bold orange1]iSearch[/] — Database: {Markup.Escape(selectedDatabase)}")
                .HighlightStyle(new Style(Color.Black, Color.Orange1))
                .UseConverter(choice => choice switch
                {
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
                    if (selectedDatabase is not null)
                    {
                        selectedReturnType = await selectReturnDatasetAsync(
                            returnTypes,
                            selectedReturnType,
                            cancellationToken).ConfigureAwait(false);
                    }

                    break;
                case DatasetChoice.ViewFields:
                    if (selectedDatabase is not null)
                    {
                        await viewFieldsAsync(selectedDatabase, cancellationToken).ConfigureAwait(false);
                    }

                    break;
                case DatasetChoice.SubmitQuery:
                    if (selectedDatabase is not null && selectedReturnType is not null)
                    {
                        await submitQueryAsync(
                            selectedDatabase,
                            selectedReturnType,
                            cancellationToken).ConfigureAwait(false);
                    }

                    break;
                case DatasetChoice.Back:
                    return;
                default:
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
            return currentReturnType;
        }

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
        return string.IsNullOrEmpty(selected) ? currentDatabase : selected;

        #endregion
    }

    /**************************************************************/
    /// <summary>Validates and submits one query for the retained database and return dataset.</summary>
    /// <param name="database">The exact selected dataset name.</param>
    /// <param name="returnType">The configured return dataset whose fields will be requested.</param>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    private async Task submitQueryAsync(
        string database,
        SearchReturnTypeDefinition returnType,
        CancellationToken cancellationToken)
    {
        #region implementation

        var query = await new TextPrompt<string>("Query [grey](free text or Lucene syntax)[/]:")
            .PromptStyle("yellow")
            .Validate(value => string.IsNullOrWhiteSpace(value)
                ? ValidationResult.Error("Query must not be empty.")
                : ValidationResult.Success())
            .ShowAsync(_console, cancellationToken)
            .ConfigureAwait(false);

        // The UI fixes the row bound and Boolean operator for the first interactive query phase.
        var result = await _client.SearchAsync(new SearchRequest
        {
            Dataset = database,
            Query = query.Trim(),
            Fields = returnType.DefaultFields,
            DefaultOp = "AND",
            Rows = 100
        }, cancellationToken).ConfigureAwait(false);

        if (result.Status == OperationStatus.Failure)
        {
            writeMessages(result.Messages);
            return;
        }

        await _resultsPager.ShowAsync(result.Value!, cancellationToken).ConfigureAwait(false);

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
