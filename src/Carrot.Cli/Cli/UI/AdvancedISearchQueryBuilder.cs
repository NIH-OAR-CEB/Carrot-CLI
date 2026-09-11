using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Carrot.Cli.Configuration;
using Carrot.Cli.ISearch.Contracts;
using Spectre.Console;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>Guides an operator through creating and reviewing an advanced iSearch request.</summary>
/// <remarks>
/// The builder keeps a request draft in memory for one prompt visit. It uses live field metadata for
/// selectable query and filter fields, validates the controls that the client can guarantee,
/// and displays the same request object that the API boundary later serializes to GET parameters.
/// </remarks>
/// <seealso cref="IAdvancedISearchQueryBuilder"/>
/// <seealso cref="SearchRequest"/>
/// <seealso cref="SearchField"/>
internal sealed class AdvancedISearchQueryBuilder : IAdvancedISearchQueryBuilder
{
    #region implementation

    private const int MaximumRows = 100;
    private const string CancelValue = "";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private readonly IAnsiConsole _console;

    /**************************************************************/
    /// <summary>Initializes the builder with the console used for all prompt and preview output.</summary>
    /// <param name="console">The console that owns the current interactive session.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="console"/> is null.</exception>
    public AdvancedISearchQueryBuilder(IAnsiConsole console)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(console);
        _console = console;

        #endregion
    }

    /**************************************************************/
    /// <summary>Builds and reviews one advanced request using the supplied live field schema.</summary>
    /// <param name="database">The exact live database selected from iSearch discovery.</param>
    /// <param name="returnType">The configured return dataset whose fields become <c>fl</c>.</param>
    /// <param name="fields">The field metadata returned for the selected database.</param>
    /// <param name="cancellationToken">The token that cancels prompt interaction.</param>
    /// <returns>The confirmed request, or <see langword="null"/> after cancellation.</returns>
    /// <remarks>
    /// A request is never returned before its JSON preview has been displayed and explicitly
    /// confirmed. Editing returns to the draft menu, so unchanged values survive each review cycle.
    /// </remarks>
    /// <seealso cref="IAdvancedISearchQueryBuilder.BuildAsync"/>
    public async Task<SearchRequest?> BuildAsync(
        string database,
        SearchReturnTypeDefinition returnType,
        IReadOnlyList<SearchField> fields,
        CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentException.ThrowIfNullOrWhiteSpace(database);
        ArgumentNullException.ThrowIfNull(returnType);
        ArgumentNullException.ThrowIfNull(fields);

        var fieldChoices = fields
            .Where(field => !string.IsNullOrWhiteSpace(field.Name))
            .GroupBy(field => field.Name, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(field => field.Name, StringComparer.Ordinal)
            .ToArray();

        // Field-qualified controls cannot be safely offered without live names; returning here
        // leaves the selected database intact while preventing an unvalidated request draft.
        if (fieldChoices.Length == 0)
        {
            _console.WriteLine("iSearch returned no usable fields for advanced query construction.");
            return null;
        }

        var draft = new QueryDraft(returnType.DefaultFields)
        {
            Dataset = database
        };
        writeHelp();

        // The menu loop keeps each setting independently editable and guarantees that the preview
        // is regenerated from the current draft immediately before confirmation.
        while (true)
        {
            writeDraftSummary(draft);
            var choice = await showDraftMenuAsync(cancellationToken).ConfigureAwait(false);

            // Each action changes only its own draft concern; the next loop iteration presents the
            // resulting state and lets the operator review or adjust another concern.
            switch (choice)
            {
                case DraftChoice.SetBaseQuery:
                    // Base-query editing keeps the request useful when no field filter is added.
                    await setBaseQueryAsync(draft, cancellationToken).ConfigureAwait(false);
                    break;
                case DraftChoice.SelectQueryFields:
                    // qf is optional and controls only unqualified terms, not the configured fl set.
                    await setQueryFieldsAsync(draft, fieldChoices, cancellationToken).ConfigureAwait(false);
                    break;
                case DraftChoice.AddFilter:
                    // Each filter is appended in operator order so the preview mirrors the draft.
                    await addFilterAsync(draft, fieldChoices, cancellationToken).ConfigureAwait(false);
                    break;
                case DraftChoice.RemoveFilter:
                    // Removal is separate from adding so a user can correct a filter without rebuilding the draft.
                    await removeFilterAsync(draft, cancellationToken).ConfigureAwait(false);
                    break;
                case DraftChoice.SetDefaultOperator:
                    // The explicit operator avoids relying on the service's historical default.
                    await setDefaultOperatorAsync(draft, cancellationToken).ConfigureAwait(false);
                    break;
                case DraftChoice.SetUpdatedDates:
                    // Date bounds are collected together so their ordering can be validated atomically.
                    await setUpdatedDatesAsync(draft, cancellationToken).ConfigureAwait(false);
                    break;
                case DraftChoice.SetRows:
                    // The row limit is validated before it can affect a service request.
                    await setRowsAsync(draft, cancellationToken).ConfigureAwait(false);
                    break;
                case DraftChoice.ReviewJson:
                    // Review creates a detached snapshot, allowing edits to continue without mutating a submitted request.
                    var request = createRequest(draft);
                    writeJsonPreview(request);
                    var reviewChoice = await showReviewMenuAsync(cancellationToken).ConfigureAwait(false);

                    // Only the explicit confirmation crosses the builder boundary; edit and cancel
                    // remain local transitions so no partially reviewed request reaches the API.
                    if (reviewChoice == ReviewChoice.Confirm)
                    {
                        return request;
                    }

                    if (reviewChoice == ReviewChoice.Cancel)
                    {
                        // Review cancellation abandons only this draft and returns control to the parent menu.
                        return null;
                    }

                    break;
                case DraftChoice.Cancel:
                    // Cancelling from the draft menu avoids both JSON review and network submission.
                    return null;
                default:
                    throw new InvalidOperationException($"Unsupported advanced-query action: {choice}");
            }
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Writes operator help for the logical iSearch search package.</summary>
    /// <remarks>The text distinguishes the body-shaped review package from the active dataset-scoped GET transport.</remarks>
    private void writeHelp()
    {
        #region implementation

        _console.WriteLine("Build Advanced Query");
        _console.WriteLine("q is the base query; blank input uses *:* so filters can stand alone.");
        _console.WriteLine("qf selects fields searched by unqualified terms; fq adds field-qualified filters.");
        _console.WriteLine("Use AND or OR explicitly, and enter updated dates in yyyy-MM-dd form.");
        _console.WriteLine("Rows must be between 1 and 100. Field names and types come from live iSearch metadata.");
        _console.WriteLine("Examples: fy:2024, fundingCategory:\"Research Project Grants\"");
        _console.WriteLine("The JSON package below is a review representation; the current client sends an encoded GET request.");

        #endregion
    }

    /**************************************************************/
    /// <summary>Displays the current draft values before the next editing decision.</summary>
    /// <param name="draft">The mutable request draft for this builder visit.</param>
    private void writeDraftSummary(QueryDraft draft)
    {
        #region implementation

        _console.WriteLine($"Base query: {draft.Query}");
        _console.WriteLine($"Query fields: {(draft.QueryFields.Count == 0 ? "iSearch defaults" : string.Join(", ", draft.QueryFields))}");
        _console.WriteLine($"Filters: {(draft.FilterQueries.Count == 0 ? "none" : string.Join("; ", draft.FilterQueries))}");
        _console.WriteLine($"Default operator: {draft.DefaultOp}; Rows: {draft.Rows}");
        _console.WriteLine($"Updated after: {draft.UpdatedAfter ?? "none"}; Updated before: {draft.UpdatedBefore ?? "none"}");

        #endregion
    }

    /**************************************************************/
    /// <summary>Prompts for the next editable advanced-query setting.</summary>
    /// <param name="cancellationToken">The token that cancels the prompt.</param>
    /// <returns>The selected draft action.</returns>
    private async Task<DraftChoice> showDraftMenuAsync(CancellationToken cancellationToken)
    {
        #region implementation

        return await new SelectionPrompt<DraftChoice>()
            .Title("Advanced Query — Edit or review")
            .HighlightStyle(new Style(Color.Black, Color.Orange1))
            .UseConverter(choice => choice switch
            {
                DraftChoice.SetBaseQuery => "Set Base Query",
                DraftChoice.SelectQueryFields => "Select Query Fields",
                DraftChoice.AddFilter => "Add Filter",
                DraftChoice.RemoveFilter => "Remove Filter",
                DraftChoice.SetDefaultOperator => "Set Default Operator",
                DraftChoice.SetUpdatedDates => "Set Updated Dates",
                DraftChoice.SetRows => "Set Rows",
                DraftChoice.ReviewJson => "Review JSON Package",
                DraftChoice.Cancel => "Cancel",
                _ => choice.ToString()
            })
            .AddChoices(Enum.GetValues<DraftChoice>())
            .AddCancelResult(DraftChoice.Cancel)
            .ShowAsync(_console, cancellationToken)
            .ConfigureAwait(false);

        #endregion
    }

    /**************************************************************/
    /// <summary>Updates the base query, converting blank input into the documented match-all query.</summary>
    /// <param name="draft">The draft to update.</param>
    /// <param name="cancellationToken">The token that cancels the prompt.</param>
    private async Task setBaseQueryAsync(QueryDraft draft, CancellationToken cancellationToken)
    {
        #region implementation

        var value = await new TextPrompt<string>("Base query [grey](blank = *:*)[/]:")
            .DefaultValue(draft.Query)
            .PromptStyle("yellow")
            .ShowAsync(_console, cancellationToken)
            .ConfigureAwait(false);

        // The match-all fallback is intentional: an advanced request may consist entirely of fq or
        // updated-date restrictions, which the service represents with q = *:*.
        draft.Query = string.IsNullOrWhiteSpace(value) ? "*:*" : value.Trim();

        #endregion
    }

    /**************************************************************/
    /// <summary>Replaces the explicit query-field selection from live field names.</summary>
    /// <param name="draft">The draft to update.</param>
    /// <param name="fields">The live selectable field definitions.</param>
    /// <param name="cancellationToken">The token that cancels the prompt.</param>
    private async Task setQueryFieldsAsync(
        QueryDraft draft,
        IReadOnlyList<SearchField> fields,
        CancellationToken cancellationToken)
    {
        #region implementation

        var prompt = new MultiSelectionPrompt<string>()
            .Title("Select fields for unqualified query terms")
            .HighlightStyle(new Style(Color.Black, Color.Orange1))
            .InstructionsText("[grey](Space selects; Enter accepts; Escape preserves the current selection)[/]")
            .NotRequired()
            .UseConverter(name => formatFieldChoice(name, fields))
            .AddChoices(fields.Select(field => field.Name));

        foreach (var currentField in draft.QueryFields)
        {
            // Preselect only fields that are still present in the live schema; stale draft state is
            // not possible on the first visit, but this guard keeps editing resilient to future reuse.
            if (fields.Any(field => string.Equals(field.Name, currentField, StringComparison.Ordinal)))
            {
                prompt.Select(currentField);
            }
        }

        var selected = await prompt
            .AddCancelResult([CancelValue])
            .ShowAsync(_console, cancellationToken)
            .ConfigureAwait(false);
        if (selected.Count == 1 && selected[0] == CancelValue)
        {
            // Escape is scoped to the qf picker, so an operator can leave the existing selection unchanged.
            return;
        }

        // MultiSelectionPrompt returns the visual order, so normalize by the live schema order for
        // deterministic request serialization and repeatable cursor continuation.
        draft.QueryFields = fields
            .Where(field => selected.Contains(field.Name, StringComparer.Ordinal))
            .Select(field => field.Name)
            .ToList();

        #endregion
    }

    /**************************************************************/
    /// <summary>Adds one field-qualified filter using a live field and type-aware input guidance.</summary>
    /// <param name="draft">The draft to update.</param>
    /// <param name="fields">The live selectable field definitions.</param>
    /// <param name="cancellationToken">The token that cancels the prompt.</param>
    private async Task addFilterAsync(
        QueryDraft draft,
        IReadOnlyList<SearchField> fields,
        CancellationToken cancellationToken)
    {
        #region implementation

        var selectedField = await new SelectionPrompt<string>()
            .Title("Select a field to filter")
            .HighlightStyle(new Style(Color.Black, Color.Orange1))
            .UseConverter(name => formatFieldChoice(name, fields))
            .AddChoices(fields.Select(field => field.Name))
            .AddCancelResult(CancelValue)
            .ShowAsync(_console, cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrEmpty(selectedField))
        {
            // Escape cancels only filter creation; previously accepted filters remain in the draft.
            return;
        }

        var field = fields.Single(item => string.Equals(item.Name, selectedField, StringComparison.Ordinal));
        var value = await promptFilterValueAsync(field, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(value))
        {
            // The value prompt can be cancelled after the field has been selected; no incomplete filter is added.
            return;
        }

        var expressionResult = createFilterExpression(field, value);
        if (expressionResult is null)
        {
            // Type validation reports the problem and leaves the operator at the draft menu.
            return;
        }

        draft.FilterQueries.Add(expressionResult);

        #endregion
    }

    /**************************************************************/
    /// <summary>Prompts for a filter value while explaining the selected field's type.</summary>
    /// <param name="field">The live field whose value is being entered.</param>
    /// <param name="cancellationToken">The token that cancels the prompt.</param>
    /// <returns>The trimmed value, or an empty string when the prompt is cancelled.</returns>
    private async Task<string> promptFilterValueAsync(SearchField field, CancellationToken cancellationToken)
    {
        #region implementation

        var fieldType = string.IsNullOrWhiteSpace(field.FieldType) ? "unknown" : field.FieldType;
        _console.WriteLine($"Field type: {fieldType}; multi-valued: {field.MultiValued?.ToString() ?? "unknown"}");
        _console.WriteLine("Enter a scalar value or a documented range expression such as [2020 TO 2024].");

        return (await new TextPrompt<string>($"Value for {Markup.Escape(field.Name)}:")
            .PromptStyle("yellow")
            .Validate(value => string.IsNullOrWhiteSpace(value)
                ? ValidationResult.Error("Filter value must not be empty.")
                : ValidationResult.Success())
            .ShowAsync(_console, cancellationToken)
            .ConfigureAwait(false)).Trim();

        #endregion
    }

    /**************************************************************/
    /// <summary>Removes one previously created filter from the draft.</summary>
    /// <param name="draft">The draft to update.</param>
    /// <param name="cancellationToken">The token that cancels the prompt.</param>
    private async Task removeFilterAsync(QueryDraft draft, CancellationToken cancellationToken)
    {
        #region implementation

        if (draft.FilterQueries.Count == 0)
        {
            _console.WriteLine("No filters are currently configured.");
            return;
        }

        var selected = await new SelectionPrompt<string>()
            .Title("Remove filter")
            .HighlightStyle(new Style(Color.Black, Color.Orange1))
            .AddChoices(draft.FilterQueries)
            .AddCancelResult(CancelValue)
            .ShowAsync(_console, cancellationToken)
            .ConfigureAwait(false);
        if (!string.IsNullOrEmpty(selected))
        {
            // Remove exactly one matching expression so duplicate-looking filters are not silently reordered.
            draft.FilterQueries.Remove(selected);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Sets the explicit Boolean operator between advanced query terms.</summary>
    /// <param name="draft">The draft to update.</param>
    /// <param name="cancellationToken">The token that cancels the prompt.</param>
    private async Task setDefaultOperatorAsync(QueryDraft draft, CancellationToken cancellationToken)
    {
        #region implementation

        draft.DefaultOp = await new SelectionPrompt<string>()
            .Title("Default operator")
            .HighlightStyle(new Style(Color.Black, Color.Orange1))
            .AddChoices("AND", "OR")
            .ShowAsync(_console, cancellationToken)
            .ConfigureAwait(false);

        #endregion
    }

    /**************************************************************/
    /// <summary>Sets both service update-date bounds as one validated draft transition.</summary>
    /// <param name="draft">The draft to update.</param>
    /// <param name="cancellationToken">The token that cancels the prompt.</param>
    private async Task setUpdatedDatesAsync(QueryDraft draft, CancellationToken cancellationToken)
    {
        #region implementation

        var updatedAfter = await promptDateAsync(
            "Updated after [grey](blank clears the bound)[/]:",
            draft.UpdatedAfter,
            cancellationToken).ConfigureAwait(false);
        var updatedBefore = await promptDateAsync(
            "Updated before [grey](blank clears the bound)[/]:",
            draft.UpdatedBefore,
            cancellationToken).ConfigureAwait(false);

        // Validate the pair before mutating either property so a reversed range cannot leave the
        // draft half-updated and produce a misleading JSON preview.
        if (updatedAfter is not null
            && updatedBefore is not null
            && DateOnly.ParseExact(updatedAfter, "yyyy-MM-dd", CultureInfo.InvariantCulture)
                > DateOnly.ParseExact(updatedBefore, "yyyy-MM-dd", CultureInfo.InvariantCulture))
        {
            // Keep both previous bounds when the new pair is reversed; the operator can correct one value explicitly.
            _console.WriteLine("Updated after must not be later than updated before.");
            return;
        }

        draft.UpdatedAfter = updatedAfter;
        draft.UpdatedBefore = updatedBefore;

        #endregion
    }

    /**************************************************************/
    /// <summary>Prompts for one optional exact update date.</summary>
    /// <param name="prompt">The displayed prompt text.</param>
    /// <param name="currentValue">The current date value, if one exists.</param>
    /// <param name="cancellationToken">The token that cancels the prompt.</param>
    /// <returns>An exact date string or <see langword="null"/> when cleared.</returns>
    private async Task<string?> promptDateAsync(
        string prompt,
        string? currentValue,
        CancellationToken cancellationToken)
    {
        #region implementation

        var value = await new TextPrompt<string>(prompt)
            .DefaultValue(currentValue ?? string.Empty)
            .PromptStyle("yellow")
            .Validate(candidate => string.IsNullOrWhiteSpace(candidate)
                || DateOnly.TryParseExact(
                    candidate.Trim(),
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out _)
                ? ValidationResult.Success()
                : ValidationResult.Error("Date must use yyyy-MM-dd format."))
            .ShowAsync(_console, cancellationToken)
            .ConfigureAwait(false);

        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        #endregion
    }

    /**************************************************************/
    /// <summary>Prompts for a bounded positive result-page size.</summary>
    /// <param name="draft">The draft to update.</param>
    /// <param name="cancellationToken">The token that cancels the prompt.</param>
    private async Task setRowsAsync(QueryDraft draft, CancellationToken cancellationToken)
    {
        #region implementation

        var value = await new TextPrompt<string>("Rows (1-100):")
            .DefaultValue(draft.Rows.ToString(CultureInfo.InvariantCulture))
            .PromptStyle("yellow")
            .Validate(candidate => int.TryParse(candidate, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rows)
                && rows is >= 1 and <= MaximumRows
                ? ValidationResult.Success()
                : ValidationResult.Error("Rows must be an integer between 1 and 100."))
            .ShowAsync(_console, cancellationToken)
            .ConfigureAwait(false);
        draft.Rows = int.Parse(value, CultureInfo.InvariantCulture);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates an immutable-by-convention request snapshot from the current draft.</summary>
    /// <param name="draft">The draft values to copy.</param>
    /// <returns>A request containing all advanced controls and configured result fields.</returns>
    private static SearchRequest createRequest(QueryDraft draft)
    {
        #region implementation

        return new SearchRequest
        {
            Dataset = draft.Dataset,
            Query = draft.Query,
            QueryFields = draft.QueryFields.Count == 0 ? null : draft.QueryFields.ToArray(),
            FilterQueries = draft.FilterQueries.Count == 0 ? null : draft.FilterQueries.ToArray(),
            Fields = draft.Fields.ToArray(),
            Rows = draft.Rows,
            DefaultOp = draft.DefaultOp,
            UpdatedAfter = draft.UpdatedAfter,
            UpdatedBefore = draft.UpdatedBefore
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>Writes the exact request snapshot used for the confirmation decision.</summary>
    /// <param name="request">The request snapshot to serialize.</param>
    private void writeJsonPreview(SearchRequest request)
    {
        #region implementation

        _console.WriteLine("JSON package to submit:");
        _console.WriteLine(JsonSerializer.Serialize(request, JsonOptions));

        #endregion
    }

    /**************************************************************/
    /// <summary>Prompts whether the reviewed request should be submitted, edited, or cancelled.</summary>
    /// <param name="cancellationToken">The token that cancels the prompt.</param>
    /// <returns>The review decision.</returns>
    private async Task<ReviewChoice> showReviewMenuAsync(CancellationToken cancellationToken)
    {
        #region implementation

        return await new SelectionPrompt<ReviewChoice>()
            .Title("Advanced query review")
            .HighlightStyle(new Style(Color.Black, Color.Orange1))
            .UseConverter(choice => choice switch
            {
                ReviewChoice.Confirm => "Confirm and Submit",
                ReviewChoice.Edit => "Edit Query Choices",
                ReviewChoice.Cancel => "Cancel",
                _ => choice.ToString()
            })
            .AddChoices(Enum.GetValues<ReviewChoice>())
            .AddCancelResult(ReviewChoice.Cancel)
            .ShowAsync(_console, cancellationToken)
            .ConfigureAwait(false);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a field label that exposes live type metadata without changing the field name.</summary>
    /// <param name="name">The live field name.</param>
    /// <param name="fields">The live field definitions.</param>
    /// <returns>A markup-safe display label.</returns>
    private static string formatFieldChoice(string name, IReadOnlyList<SearchField> fields)
    {
        #region implementation

        var field = fields.Single(item => string.Equals(item.Name, name, StringComparison.Ordinal));
        var type = string.IsNullOrWhiteSpace(field.FieldType) ? "unknown" : field.FieldType;
        return $"{Markup.Escape(name)} ({Markup.Escape(type)})";

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one validated field-qualified filter expression.</summary>
    /// <param name="field">The live field selected by the operator.</param>
    /// <param name="value">The entered scalar or documented range expression.</param>
    /// <returns>A field-qualified expression, or <see langword="null"/> when the value is invalid.</returns>
    private string? createFilterExpression(SearchField field, string value)
    {
        #region implementation

        var fieldType = field.FieldType?.Trim().ToLowerInvariant() ?? string.Empty;
        var normalizedValue = value.Trim();
        var isRange = (normalizedValue.StartsWith("[", StringComparison.Ordinal)
                && normalizedValue.EndsWith("]", StringComparison.Ordinal))
            || (normalizedValue.StartsWith("{", StringComparison.Ordinal)
                && normalizedValue.EndsWith("}", StringComparison.Ordinal));

        // Numeric and Boolean fields are kept unquoted so iSearch receives the type it advertises;
        // ranges remain intact because their brackets and TO operator are query syntax, not data.
        if (isRange || isNumericType(fieldType))
        {
            if (!isRange && !isNumericValue(normalizedValue))
            {
                _console.WriteLine($"The value for {field.Name} must be numeric or a documented range.");
                return null;
            }

            return $"{field.Name}:{normalizedValue}";
        }

        if (isBooleanType(fieldType)
            && !new[] { "yes", "no", "true", "false", "1", "0" }
                .Contains(normalizedValue, StringComparer.OrdinalIgnoreCase))
        {
            _console.WriteLine($"The value for {field.Name} must be Yes, No, True, False, 1, or 0.");
            return null;
        }

        // Phrase values are quoted and escaped only for string-like fields; this keeps spaces in
        // category labels together while allowing an operator to provide an already documented date
        // or range expression without lossy rewriting.
        var escaped = normalizedValue.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
        var formattedValue = escaped.Contains(' ') || escaped.Contains(':')
            ? $"\"{escaped}\""
            : escaped;
        return $"{field.Name}:{formattedValue}";

        #endregion
    }

    /**************************************************************/
    /// <summary>Determines whether a service field type represents a numeric value.</summary>
    /// <param name="fieldType">The service-returned type string.</param>
    /// <returns><see langword="true"/> for the documented numeric type families.</returns>
    private static bool isNumericType(string fieldType)
    {
        #region implementation

        return fieldType.Contains("int", StringComparison.Ordinal)
            || fieldType.Contains("long", StringComparison.Ordinal)
            || fieldType.Contains("float", StringComparison.Ordinal)
            || fieldType.Contains("double", StringComparison.Ordinal)
            || fieldType.Contains("numeric", StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>Determines whether a service field type represents a Boolean value.</summary>
    /// <param name="fieldType">The service-returned type string.</param>
    /// <returns><see langword="true"/> when the type identifies a Boolean field.</returns>
    private static bool isBooleanType(string fieldType)
    {
        #region implementation

        return fieldType is "bool" or "boolean";

        #endregion
    }

    /**************************************************************/
    /// <summary>Checks one entered numeric scalar using invariant culture.</summary>
    /// <param name="value">The candidate filter value.</param>
    /// <returns><see langword="true"/> when the value is an integer or floating-point number.</returns>
    private static bool isNumericValue(string value)
    {
        #region implementation

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

        #endregion
    }

    /**************************************************************/
    /// <summary>Defines the editable settings in the advanced-query draft menu.</summary>
    private enum DraftChoice
    {
        /**************************************************************/
        /// <summary>Changes the base q expression.</summary>
        SetBaseQuery,

        /**************************************************************/
        /// <summary>Selects fields used for unqualified terms.</summary>
        SelectQueryFields,

        /**************************************************************/
        /// <summary>Adds one field-qualified filter expression.</summary>
        AddFilter,

        /**************************************************************/
        /// <summary>Removes one existing field-qualified filter.</summary>
        RemoveFilter,

        /**************************************************************/
        /// <summary>Changes the explicit AND or OR operator.</summary>
        SetDefaultOperator,

        /**************************************************************/
        /// <summary>Changes or clears service update-date bounds.</summary>
        SetUpdatedDates,

        /**************************************************************/
        /// <summary>Changes the bounded service page size.</summary>
        SetRows,

        /**************************************************************/
        /// <summary>Displays the logical JSON package for confirmation.</summary>
        ReviewJson,

        /**************************************************************/
        /// <summary>Abandons advanced query construction.</summary>
        Cancel
    }

    /**************************************************************/
    /// <summary>Defines the decisions available after a JSON package is displayed.</summary>
    private enum ReviewChoice
    {
        /**************************************************************/
        /// <summary>Returns the reviewed request to the interactive flow for submission.</summary>
        Confirm,

        /**************************************************************/
        /// <summary>Returns to the draft menu while preserving all current values.</summary>
        Edit,

        /**************************************************************/
        /// <summary>Abandons the reviewed request without submission.</summary>
        Cancel
    }

    /**************************************************************/
    /// <summary>Holds mutable prompt state until the operator confirms a request.</summary>
    /// <remarks>All collections belong to this one builder visit and are copied into SearchRequest at review time.</remarks>
    private sealed class QueryDraft
    {
        #region implementation

        /**************************************************************/
        /// <summary>Initializes a draft with the configured result-field set and safe defaults.</summary>
        /// <param name="fields">The ordered configured fields used for the request's result set.</param>
        public QueryDraft(IReadOnlyList<string> fields)
        {
            #region implementation

            Fields = fields.ToArray();

            #endregion
        }

        /**************************************************************/
        /// <summary>Gets or sets the exact live database name.</summary>
        public string Dataset { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>Gets or sets the base query, defaulting to the match-all expression.</summary>
        public string Query { get; set; } = "*:*";

        /**************************************************************/
        /// <summary>Gets or sets the ordered fields used for unqualified terms.</summary>
        public List<string> QueryFields { get; set; } = [];

        /**************************************************************/
        /// <summary>Gets or sets the ordered field-qualified filters.</summary>
        public List<string> FilterQueries { get; set; } = [];

        /**************************************************************/
        /// <summary>Gets or sets the configured result-field list.</summary>
        public string[] Fields { get; }

        /**************************************************************/
        /// <summary>Gets or sets the explicit default Boolean operator.</summary>
        public string DefaultOp { get; set; } = "AND";

        /**************************************************************/
        /// <summary>Gets or sets the optional lower update-date bound.</summary>
        public string? UpdatedAfter { get; set; }

        /**************************************************************/
        /// <summary>Gets or sets the optional upper update-date bound.</summary>
        public string? UpdatedBefore { get; set; }

        /**************************************************************/
        /// <summary>Gets or sets the bounded number of records requested per service page.</summary>
        public int Rows { get; set; } = MaximumRows;

        #endregion
    }

    #endregion
}
