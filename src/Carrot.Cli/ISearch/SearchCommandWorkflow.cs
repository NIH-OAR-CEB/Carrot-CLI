using Carrot.Cli.CarrotApi;
using Carrot.Cli.Cli.Settings;
using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Carrot.Cli.Reporting;
using Carrot.Cli.ISearch.Contracts;
using Microsoft.Extensions.Options;

namespace Carrot.Cli.ISearch;

/**************************************************************/
/// <summary>Coordinates the prompt-free iSearch search, paging, categorization, and export pipeline.</summary>
/// <remarks>
/// The workflow owns ordering and live validation while the API client, page session, categorizer,
/// and exporters retain their existing responsibilities. No prompt is displayed by this boundary.
/// </remarks>
/// <seealso cref="ISearchCommandWorkflow"/>
/// <seealso cref="SearchResultPageSession"/>
internal sealed class SearchCommandWorkflow : ISearchCommandWorkflow
{
    #region implementation

    private readonly IISearchApiClient _client;
    private readonly ISearchOptions _options;
    private readonly ISearchOptionsValidator _optionsValidator;
    private readonly SearchReturnTypeCatalog _returnTypeCatalog;
    private readonly RunSettingsResolver _settingsResolver;
    private readonly ExcelOutputPathResolver _pathResolver;
    private readonly ISearchResultsExporter _resultsExporter;
    private readonly ISearchResultsCategorizer _categorizer;
    private readonly ICategorizedISearchResultsExporter _categorizedExporter;

    /**************************************************************/
    /// <summary>Initializes named iSearch orchestration with existing feature boundaries.</summary>
    /// <param name="client">The authenticated iSearch API client.</param>
    /// <param name="options">The configured iSearch credentials and safety settings.</param>
    /// <param name="optionsValidator">The credential validator.</param>
    /// <param name="returnTypeCatalog">The configured return-field catalog.</param>
    /// <param name="settingsResolver">The Carrot endpoint and timeout resolver.</param>
    /// <param name="pathResolver">The Excel destination validator.</param>
    /// <param name="resultsExporter">The original-result exporter.</param>
    /// <param name="categorizer">The shared iSearch-to-Carrot adapter.</param>
    /// <param name="categorizedExporter">The categorized-result exporter.</param>
    /// <exception cref="ArgumentNullException">Thrown when a dependency is null.</exception>
    public SearchCommandWorkflow(
        IISearchApiClient client,
        IOptions<ISearchOptions> options,
        ISearchOptionsValidator optionsValidator,
        SearchReturnTypeCatalog returnTypeCatalog,
        RunSettingsResolver settingsResolver,
        ExcelOutputPathResolver pathResolver,
        ISearchResultsExporter resultsExporter,
        ISearchResultsCategorizer categorizer,
        ICategorizedISearchResultsExporter categorizedExporter)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(optionsValidator);
        ArgumentNullException.ThrowIfNull(returnTypeCatalog);
        ArgumentNullException.ThrowIfNull(settingsResolver);
        ArgumentNullException.ThrowIfNull(pathResolver);
        ArgumentNullException.ThrowIfNull(resultsExporter);
        ArgumentNullException.ThrowIfNull(categorizer);
        ArgumentNullException.ThrowIfNull(categorizedExporter);
        _client = client;
        _options = options.Value;
        _optionsValidator = optionsValidator;
        _returnTypeCatalog = returnTypeCatalog;
        _settingsResolver = settingsResolver;
        _pathResolver = pathResolver;
        _resultsExporter = resultsExporter;
        _categorizer = categorizer;
        _categorizedExporter = categorizedExporter;

        #endregion
    }

    /**************************************************************/
    /// <summary>Runs one named iSearch operation without prompting.</summary>
    /// <param name="request">The parsed command values.</param>
    /// <param name="cancellationToken">The token that cancels every downstream operation.</param>
    /// <returns>A complete, partial, or failed command result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    public async Task<OperationResult<SearchCommandResult>> RunAsync(
        SearchCommandRequest request,
        CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var localResult = resolveLocalInputs(request);
            if (localResult.Status == OperationStatus.Failure)
            {
                return failure<SearchCommandResult>(localResult.Messages);
            }

            var local = localResult.Value!;
            var credentialResult = _optionsValidator.Validate(_options);
            if (credentialResult.Status == OperationStatus.Failure)
            {
                return failure<SearchCommandResult>(credentialResult.Messages);
            }

            var endpointResult = resolveEndpoint(request);
            if (endpointResult.Status == OperationStatus.Failure)
            {
                return failure<SearchCommandResult>(endpointResult.Messages);
            }

            var healthResult = await _client.GetHealthAsync(cancellationToken).ConfigureAwait(false);
            if (healthResult.Status == OperationStatus.Failure)
            {
                return failure<SearchCommandResult>(healthResult.Messages);
            }

            // Dataset discovery is allowed only after the service explicitly reports readiness.
            if (!string.Equals(healthResult.Value!.Status, "UP", StringComparison.OrdinalIgnoreCase))
            {
                return failure<SearchCommandResult>(
                [message("isearch.health.unavailable", "iSearch is not available; dataset discovery was not attempted.")]);
            }

            var datasetsResult = await _client.GetDatasetsAsync(cancellationToken).ConfigureAwait(false);
            if (datasetsResult.Status == OperationStatus.Failure)
            {
                return failure<SearchCommandResult>(datasetsResult.Messages);
            }

            if (!datasetsResult.Value!.Contains(request.Database, StringComparer.Ordinal))
            {
                return failure<SearchCommandResult>(
                [message("isearch.database.unknown", $"The live iSearch database was not found: {request.Database}.")]);
            }

            var returnTypesResult = _returnTypeCatalog.GetConfiguration();
            if (returnTypesResult.Status == OperationStatus.Failure)
            {
                return failure<SearchCommandResult>(returnTypesResult.Messages);
            }

            var returnType = returnTypesResult.Value!.ReturnTypes
                .FirstOrDefault(item => string.Equals(item.Name, request.ReturnDataset, StringComparison.Ordinal));
            if (returnType is null)
            {
                return failure<SearchCommandResult>(
                [message(
                    "isearch.return-type.unknown",
                    $"The configured iSearch return dataset was not found: {request.ReturnDataset}.")]);
            }

            var normalizedFields = await resolveFieldReferencesAsync(
                request,
                cancellationToken).ConfigureAwait(false);
            if (normalizedFields.Status == OperationStatus.Failure)
            {
                return failure<SearchCommandResult>(normalizedFields.Messages);
            }

            var searchRequest = new SearchRequest
            {
                Dataset = request.Database,
                Query = request.Query.Trim(),
                QueryFields = normalizedFields.Value!.QueryFields,
                FilterQueries = normalizedFields.Value.FilterQueries,
                Fields = returnType.DefaultFields,
                DefaultOp = request.DefaultOp,
                Rows = request.Rows,
                UpdatedAfter = request.UpdatedAfter,
                UpdatedBefore = request.UpdatedBefore
            };

            var initialResult = await _client.SearchAsync(searchRequest, cancellationToken).ConfigureAwait(false);
            if (initialResult.Status == OperationStatus.Failure)
            {
                return failure<SearchCommandResult>(initialResult.Messages);
            }

            var session = new SearchResultPageSession(
                _client,
                searchRequest,
                initialResult.Value!,
                request.ReturnDataset,
                request.AllResults ? null : local.MaxResults);

            var walkMessages = new List<OperationMessage>();
            var walkStatus = await walkAsync(session, local.MaxResults, request.AllResults, walkMessages, cancellationToken)
                .ConfigureAwait(false);
            if (walkStatus.Status == OperationStatus.Failure)
            {
                return failure<SearchCommandResult>(walkStatus.Messages);
            }

            walkMessages.AddRange(walkStatus.Messages);

            var artifactPaths = new List<string>();
            if (local.OutputPath is not null)
            {
                var outputResult = await _resultsExporter.SaveAsync(
                    new SaveISearchResultsRequest
                    {
                        Session = session,
                        OutputPath = local.OutputPath,
                        Overwrite = request.Overwrite
                    },
                    cancellationToken).ConfigureAwait(false);
                if (outputResult.Status == OperationStatus.Failure)
                {
                    return failure<SearchCommandResult>(outputResult.Messages);
                }

                artifactPaths.Add(outputResult.Value!);
            }

            CategorizedISearchResultBatch? categorizedBatch = null;
            if (request.Categorize)
            {
                var categorizedResult = await _categorizer.CategorizeAsync(
                    session,
                    endpointResult.Value!.Endpoint,
                    endpointResult.Value.Timeout,
                    cancellationToken).ConfigureAwait(false);
                if (categorizedResult.Status == OperationStatus.Failure)
                {
                    return failure<SearchCommandResult>(categorizedResult.Messages);
                }

                categorizedBatch = categorizedResult.Value!;
                if (local.CategorizedOutputPath is not null)
                {
                    var categorizedOutputResult = await _categorizedExporter.SaveAsync(
                        new SaveCategorizedISearchResultsRequest
                        {
                            Batch = categorizedBatch,
                            OutputPath = local.CategorizedOutputPath,
                            Overwrite = request.Overwrite
                        },
                        cancellationToken).ConfigureAwait(false);
                    if (categorizedOutputResult.Status == OperationStatus.Failure)
                    {
                        return failure<SearchCommandResult>(categorizedOutputResult.Messages);
                    }

                    artifactPaths.Add(categorizedOutputResult.Value!);
                }
            }

            var result = new SearchCommandResult
            {
                Database = request.Database,
                ReturnDataset = request.ReturnDataset,
                Session = session,
                CategorizedBatch = categorizedBatch,
                ArtifactPaths = Array.AsReadOnly(artifactPaths.ToArray()),
                IsComplete = session.WalkProgress.IsComplete
            };

            if (walkMessages.Count > 0)
            {
                return OperationResult<SearchCommandResult>.PartialSuccess(result, walkMessages);
            }

            return OperationResult<SearchCommandResult>.Success(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Resolves local paths, bounds, and optional output relationships before network work.</summary>
    /// <param name="request">The parsed command request.</param>
    /// <returns>Normalized paths and the effective retained-record bound.</returns>
    private OperationResult<LocalInputs> resolveLocalInputs(SearchCommandRequest request)
    {
        #region implementation

        var maxResults = request.MaxResults ?? 100;
        if (maxResults <= 0 || request.Rows is < 1 or > 100)
        {
            return failure<LocalInputs>([message("isearch.request.invalid", "iSearch result bounds are invalid.")]);
        }

        if (request.AllResults && request.MaxResults.HasValue)
        {
            return failure<LocalInputs>([message("isearch.request.conflict", "--all-results cannot be combined with --max-results.")]);
        }

        string? outputPath = null;
        if (request.OutputPath is not null)
        {
            var outputResult = _pathResolver.Resolve(request.OutputPath);
            if (outputResult.Status == OperationStatus.Failure)
            {
                return failure<LocalInputs>(outputResult.Messages);
            }

            outputPath = outputResult.Value!;
        }

        string? categorizedOutputPath = null;
        if (request.CategorizedOutputPath is not null)
        {
            var categorizedOutputResult = _pathResolver.Resolve(request.CategorizedOutputPath);
            if (categorizedOutputResult.Status == OperationStatus.Failure)
            {
                return failure<LocalInputs>(categorizedOutputResult.Messages);
            }

            categorizedOutputPath = categorizedOutputResult.Value!;
        }

        if (categorizedOutputPath is not null && !request.Categorize)
        {
            return failure<LocalInputs>([message("isearch.categorization.output", "--categorized-output requires --categorize.")]);
        }

        if (outputPath is not null
            && categorizedOutputPath is not null
            && pathsEqual(outputPath, categorizedOutputPath))
        {
            return failure<LocalInputs>([message("isearch.output.conflict", "Original and categorized output paths must differ.")]);
        }

        return OperationResult<LocalInputs>.Success(new LocalInputs
        {
            MaxResults = maxResults,
            OutputPath = outputPath,
            CategorizedOutputPath = categorizedOutputPath
        });

        #endregion
    }

    /**************************************************************/
    /// <summary>Resolves the optional Carrot endpoint only when categorization is requested.</summary>
    /// <param name="request">The parsed command request.</param>
    /// <returns>The resolved endpoint settings or a safe configuration failure.</returns>
    private OperationResult<RunSettingsResolver.ResolvedEndpointSettings> resolveEndpoint(SearchCommandRequest request)
    {
        #region implementation

        if (!request.Categorize)
        {
            return OperationResult<RunSettingsResolver.ResolvedEndpointSettings>.Success(
                new RunSettingsResolver.ResolvedEndpointSettings
                {
                    Endpoint = new Uri("http://localhost:8080/service", UriKind.Absolute),
                    Timeout = TimeSpan.Zero
                });
        }

        return _settingsResolver.ResolveEndpointSettings(new EndpointSettings
        {
            Endpoint = request.Endpoint,
            TimeoutSeconds = request.TimeoutSeconds
        });

        #endregion
    }

    /**************************************************************/
    /// <summary>Discovers and validates live field references used by advanced options.</summary>
    /// <param name="request">The parsed command request.</param>
    /// <param name="cancellationToken">The token that cancels field discovery.</param>
    /// <returns>Normalized ordered query fields and filter expressions.</returns>
    private async Task<OperationResult<NormalizedFields>> resolveFieldReferencesAsync(
        SearchCommandRequest request,
        CancellationToken cancellationToken)
    {
        #region implementation

        var queryFields = request.QueryFields
            .Select(value => value.Trim())
            .ToArray();
        var filters = request.FilterQueries
            .Select(value => value.Trim())
            .ToArray();

        if (queryFields.Any(string.IsNullOrWhiteSpace) || filters.Any(string.IsNullOrWhiteSpace))
        {
            return failure<NormalizedFields>([message("isearch.field.empty", "Query fields and filter queries must not be blank.")]);
        }

        if (queryFields.Length == 0 && filters.Length == 0)
        {
            return OperationResult<NormalizedFields>.Success(new NormalizedFields
            {
                QueryFields = null,
                FilterQueries = null
            });
        }

        var fieldsResult = await _client.GetFieldsAsync(request.Database, cancellationToken).ConfigureAwait(false);
        if (fieldsResult.Status == OperationStatus.Failure)
        {
            return failure<NormalizedFields>(fieldsResult.Messages);
        }

        var fieldNames = fieldsResult.Value!
            .Select(field => field.Name)
            .ToHashSet(StringComparer.Ordinal);
        if (queryFields.Any(field => !fieldNames.Contains(field)))
        {
            var unknown = queryFields.First(field => !fieldNames.Contains(field));
            return failure<NormalizedFields>([message("isearch.field.unknown", $"The iSearch query field was not found: {unknown}.")]);
        }

        foreach (var filter in filters)
        {
            var field = getFilterField(filter);
            if (field is null)
            {
                return failure<NormalizedFields>([message("isearch.filter.invalid", $"The filter must begin with a live field name followed by ':': {filter}")]);
            }

            if (!fieldNames.Contains(field))
            {
                return failure<NormalizedFields>([message("isearch.field.unknown", $"The iSearch filter field was not found: {field}.")]);
            }
        }

        return OperationResult<NormalizedFields>.Success(new NormalizedFields
        {
            QueryFields = queryFields.Length == 0 ? null : Array.AsReadOnly(queryFields),
            FilterQueries = filters.Length == 0 ? null : Array.AsReadOnly(filters)
        });

        #endregion
    }

    /**************************************************************/
    /// <summary>Walks the session to the requested bounded target or service total.</summary>
    /// <param name="session">The initialized iSearch page session.</param>
    /// <param name="maxResults">The effective retained-record bound.</param>
    /// <param name="allResults">Whether the stable service total is the target.</param>
    /// <param name="messages">The warning collection for useful bounded partial results.</param>
    /// <param name="cancellationToken">The token that cancels continuation requests.</param>
    /// <returns>A successful or failed walk status.</returns>
    private static async Task<OperationResult<bool>> walkAsync(
        SearchResultPageSession session,
        int maxResults,
        bool allResults,
        List<OperationMessage> messages,
        CancellationToken cancellationToken)
    {
        #region implementation

        if (allResults)
        {
            var allResult = await session.FetchAllAsync(progress: null, cancellationToken).ConfigureAwait(false);
            return allResult.Status == OperationStatus.Failure
                ? failure<bool>(allResult.Messages)
                : OperationResult<bool>.Success(true);
        }

        while (!session.WalkProgress.IsComplete
            && session.WalkedResults.Count < maxResults
            && session.CanFetchNextPage)
        {
            var nextResult = await session.FetchNextAsync(cancellationToken).ConfigureAwait(false);
            if (nextResult.Status == OperationStatus.Failure)
            {
                return session.WalkedResults.Count > 0
                    ? OperationResult<bool>.PartialSuccess(true, nextResult.Messages)
                    : failure<bool>(nextResult.Messages);
            }
        }

        if (!session.WalkProgress.IsComplete && session.WalkedResults.Count >= maxResults)
        {
            messages.Add(message(
                "isearch.search.partial",
                $"The result walk stopped at the requested bound of {maxResults:N0} records.",
                OperationMessageSeverity.Warning));
        }
        else if (!session.WalkProgress.IsComplete)
        {
            return failure<bool>([message("isearch.paging.incomplete", "iSearch stopped page continuation before the requested result walk completed.")]);
        }

        return OperationResult<bool>.Success(true);

        #endregion
    }

    /**************************************************************/
    /// <summary>Extracts the leading field name from one complete fq expression.</summary>
    /// <param name="filter">The trimmed filter expression.</param>
    /// <returns>The leading field name or null when the expression is not field-qualified.</returns>
    private static string? getFilterField(string filter)
    {
        #region implementation

        var separator = filter.IndexOf(':');
        if (separator <= 0)
        {
            return null;
        }

        var field = filter[..separator].Trim();
        return field.Length == 0 || field.Any(char.IsWhiteSpace) ? null : field;

        #endregion
    }

    /**************************************************************/
    /// <summary>Compares normalized paths without treating case differences as separate files.</summary>
    /// <param name="left">The first absolute path.</param>
    /// <param name="right">The second absolute path.</param>
    /// <returns>True when both paths identify the same destination.</returns>
    private static bool pathsEqual(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    /**************************************************************/
    /// <summary>Creates a failed operation result with supplied diagnostics.</summary>
    /// <typeparam name="T">The expected result type.</typeparam>
    /// <param name="messages">The nonempty failure diagnostics.</param>
    /// <returns>A failed operation result.</returns>
    private static OperationResult<T> failure<T>(IReadOnlyList<OperationMessage> messages) =>
        OperationResult<T>.Failure(messages);

    /**************************************************************/
    /// <summary>Creates one operation diagnostic.</summary>
    /// <param name="code">The stable code.</param>
    /// <param name="text">The safe message.</param>
    /// <param name="severity">The diagnostic severity.</param>
    /// <returns>The operation message.</returns>
    private static OperationMessage message(
        string code,
        string text,
        OperationMessageSeverity severity = OperationMessageSeverity.Error) =>
        new() { Code = code, Message = text, Severity = severity };

    /**************************************************************/
    /// <summary>Carries normalized local paths and the effective bounded result count.</summary>
    private sealed record LocalInputs
    {
        #region implementation

        /**************************************************************/
        /// <summary>Gets the effective retained-record bound.</summary>
        public required int MaxResults { get; init; }

        /**************************************************************/
        /// <summary>Gets the normalized original workbook path.</summary>
        public string? OutputPath { get; init; }

        /**************************************************************/
        /// <summary>Gets the normalized categorized workbook path.</summary>
        public string? CategorizedOutputPath { get; init; }

        #endregion
    }

    /**************************************************************/
    /// <summary>Carries normalized advanced field references for the shared request model.</summary>
    private sealed record NormalizedFields
    {
        #region implementation

        /**************************************************************/
        /// <summary>Gets ordered query fields or null when omitted.</summary>
        public IReadOnlyList<string>? QueryFields { get; init; }

        /**************************************************************/
        /// <summary>Gets ordered filter expressions or null when omitted.</summary>
        public IReadOnlyList<string>? FilterQueries { get; init; }

        #endregion
    }

    #endregion
}
