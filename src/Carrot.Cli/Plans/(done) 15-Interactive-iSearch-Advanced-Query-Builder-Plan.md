# Interactive iSearch Advanced Query Builder

**Status:** Complete

## 1. Outcome

Extend the selected-database iSearch submenu with **Build Advanced Query**, alongside the existing **Submit Query** action. The advanced workflow will use live field metadata to guide the operator through building a bounded iSearch request with a base query, query-field selection, one or more field filters, Boolean operator, result-page size, and service update-date bounds. It will support common cases such as fiscal-year and category filters through fq and updated-date limits through updatedAfter/updatedBefore.

Before submitting, the CLI will display the complete logical iSearch JSON request package as pretty-printed JSON. The operator can choose **Edit** to revisit a setting, **Confirm and Submit** to run the request, or **Cancel** to return to the selected-database menu. Confirmed requests will reuse the existing authenticated search client and result-page session, including the selected live database, configured return fields, cursor paging, cancellation, and safe error behavior.

## 2. Problem

The current interactive flow has a single query path in src/Carrot.Cli/Cli/UI/InteractiveISearchFlow.cs: submitQueryAsync prompts only for nonempty raw query text and creates a SearchRequest containing Dataset, Query, the selected return dataset's Fields, DefaultOp = "AND", and Rows = 100. The selected-dataset menu currently offers **Select Database**, **Select Return Dataset**, **View Fields**, and **Submit Query**, but no guided way to express field filters or date constraints.

The repository already has the necessary discovery and result foundations:

- IISearchApiClient.GetFieldsAsync retrieves live SearchField metadata from GET /fields/{dataset}.
- SearchField exposes the service-owned field name, type, query/result flags, and multi-value metadata; the prior field-discovery plan deliberately deferred type-aware query assistance.
- SearchRequest and ISearchApiClient.createSearchUri currently support only the basic q, defaultOp, rows, and configured fl values.
- SearchResultPageSession already keeps the stable request separate from the service cursor, so advanced request values can flow through later pages without becoming UI state.

As a result, operators must manually know and type Lucene-like field expressions, cannot select from the discovered schema, cannot set the documented fq, qf, or update-date controls, and cannot review the final request package before making a network call.

## 3. Solution vision

Keep InteractiveISearchFlow as the iSearch visit and menu coordinator, but move advanced prompt state and formatting into a focused IAdvancedISearchQueryBuilder/AdvancedISearchQueryBuilder boundary. The builder will receive the selected live database, configured return-field definition, and the fields returned by GetFieldsAsync; it will own help text, prompts, validation, draft state, pretty JSON review, and edit/confirm/cancel navigation. It will not call HTTP.

The flow will remain:

1. Validate local iSearch configuration, check health, discover live databases, and retain the selected database and configured return dataset as today.
2. Leave **Submit Query** unchanged for the basic free-text workflow.
3. When the operator chooses **Build Advanced Query**, retrieve the current database fields through the existing API boundary. The builder presents field names and types from that response rather than hard-coding a database schema.
4. Build a SearchRequest whose JSON names mirror the iSearch POST /search body contract: dataset, q, qf, fq, fl, rows, defaultOp, updatedBefore, and updatedAfter. cursor is deliberately absent from the preview because it belongs to later result-page continuation, and content-type is transport metadata rather than a query choice.
5. Show that request with WriteIndented = true. On **Edit**, return to a menu that identifies each current setting and lets the operator change one setting without losing the other draft values. On **Confirm and Submit**, call the existing SearchAsync boundary and pass the response to the existing SearchResultPageSession/ISearchResultsPager path.

The request model remains the shared contract between the UI and API layers. The UI knows how to collect a valid draft; the API client knows how to encode optional arrays and date controls for the repository's current dataset-scoped GET /search/{dataset} implementation. The preview is body-shaped because it is the clearest representation of the documented Build searches form, while the live request remains GET: the completed iSearch work records that the live body-based POST operation currently returns HTTP 500 even for *:*.

## 4. Scope

- Add an advanced-query action to the selected-database menu without changing the basic query action's prompts or request defaults.
- Extend the internal search request contract with optional query fields, filter queries, update-date bounds, and an advanced row limit while preserving the existing configured return-field list.
- Add a focused prompt-driven advanced-query builder using live SearchField metadata, visible field/type help, type-appropriate value validation, and menu-based editing.
- Serialize the confirmed request's logical JSON package for review and extend the current GET serializer to send the corresponding optional query parameters.
- Add focused API, builder, and interactive-flow tests, update iSearch help/reference material, and verify the full repository checks.

## 5. Non-goals

- Do not remove, redesign, or change the behavior of **Submit Query** for basic free-text queries.
- Do not change iSearch authentication, health gating, dataset discovery, return-dataset configuration, result rendering, cursor semantics, export behavior, or Carrot categorization.
- Do not switch the live search operation from GET to POST until a separate verified service-contract decision resolves the currently documented POST failure.
- Do not expose or let the operator edit cursor, API credentials, cookies, HTTP headers, or transport-only content-type metadata.
- Do not hard-code a complete iSearch field catalog, database-specific fiscal-year/category names, field types, or synonym variants. Field choices must come from the selected database's live discovery response.
- Do not add saved searches, query persistence, named command-line options, arbitrary unbounded paging, query-result export changes, or a general-purpose JSON editor.
- Do not introduce a new database-specific request model or a second authenticated HTTP client.

## 6. Technical approach

### Request contract and JSON preview

Extend src/Carrot.Cli/ISearch/Contracts/SearchRequest.cs with optional properties that map to the iSearch Build searches body. Keep the existing Fields property as the configured result-field collection and annotate the model so the review JSON uses the documented names. Optional empty/null collections and date values must be omitted from the package; q, dataset, fl, rows, and defaultOp remain present for every confirmed request.

An illustrative contract shape is:

    internal sealed class SearchRequest
    {
        [JsonPropertyName("dataset")]
        public string Dataset { get; set; } = string.Empty;

        [JsonPropertyName("q")]
        public string Query { get; set; } = "*:*";

        [JsonPropertyName("qf")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<string>? QueryFields { get; set; }

        [JsonPropertyName("fq")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<string>? FilterQueries { get; set; }

        [JsonPropertyName("fl")]
        public IReadOnlyList<string> Fields { get; set; } = Array.Empty<string>();

        [JsonPropertyName("rows")]
        public int Rows { get; set; } = 100;

        [JsonPropertyName("defaultOp")]
        public string DefaultOp { get; set; } = "AND";

        [JsonPropertyName("updatedBefore")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UpdatedBefore { get; set; }

        [JsonPropertyName("updatedAfter")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UpdatedAfter { get; set; }
    }

The exact null/empty representation must match the repository's serializer behavior and the pinned .NET 10 compiler; the implementation should avoid emitting optional properties merely to show empty arrays. The preview should use a shared static JsonSerializerOptions instance with WriteIndented = true and write literal console text so query/filter values cannot be interpreted as Spectre markup.

### Prompt and filter semantics

AdvancedISearchQueryBuilder should begin with a short help panel that explains the request parts, the *:* match-all default, qf versus fq, AND/OR, the YYYY-MM-DD format for updatedAfter/updatedBefore, the 1–100 row bound, and examples such as:

    Base query: *:*
    Filter: fy:2024
    Filter: fundingCategory:"Research Project Grants"
    Updated after: 2024-10-01
    Updated before: 2025-09-30

These are explanatory examples only; field names must be offered from the live metadata and the implementation must verify the exact date/range expression rules against the Zulia query syntax documentation linked by iSearch before coding the formatter.

Use a menu-driven draft editor with actions such as **Set Base Query**, **Select Query Fields**, **Add Filter**, **Remove Filter**, **Set Default Operator**, **Set Updated Date Bounds**, **Set Rows**, **Review JSON**, and **Cancel**. Use the installed Spectre.Console 0.55.0 prompt APIs already used by the repository; if multi-selection is used for qf, verify the exact API against the resolved package and retain a repeatable single-selection fallback if necessary.

- **Base query (q)**: allow blank input to become *:*, or accept nonempty free text/Lucene syntax after trimming.
- **Query fields (qf)**: allow zero or more live field names for unqualified terms. Preserve the operator's selected order and omit qf when no explicit fields are chosen.
- **Field filters (fq)**: prompt for a live field, then prompt for a value/expression using help based on fieldType and multiValued. Compose a field-qualified filter with the documented escaping/quoting rules; support repeated filters and removal. Numeric fiscal-year values should remain numeric, Boolean values should use the service-supported Boolean forms, and string/category phrases containing spaces should be quoted safely. Date/range input must use verified Zulia syntax, not an invented parser.
- **Default operator**: present AND and OR, defaulting to AND so advanced requests are explicit rather than dependent on service defaults.
- **Update dates**: prompt independently for updatedAfter and updatedBefore in exact yyyy-MM-dd format. Reject invalid dates and reject a range whose lower bound is after its upper bound. These controls remain distinct from a field-specific date range in fq.
- **Rows**: retain the existing safe maximum of 100 but let advanced users choose a positive value from 1 through 100; use the existing basic-query value of 100 when the operator accepts the default.

The builder returns either a complete SearchRequest or an explicit cancel outcome. It should not return a partially valid request to the flow. Each review/edit cycle must rebuild the preview from the current draft so the displayed package is exactly what **Confirm and Submit** will send.

### HTTP serialization and page continuation

Update ISearchApiClient.searchAsync/createSearchUri to append only the optional values that are present. Encode each complete query-string value with Uri.EscapeDataString; serialize array controls using the iSearch GET convention of comma-separated values with explode: false. Preserve the current q, defaultOp, rows, and fl behavior for basic requests and add qf, repeated logical fq values, updatedBefore, and updatedAfter only for advanced requests.

The stable request passed into SearchResultPageSession must contain every advanced value. A cursor continuation may add only its service cursor; it must not rebuild the query from prompts or lose filters, dates, operator, row size, or result fields. Existing request validation remains the execution boundary: dataset and query are nonempty, defaultOp is AND or OR, fields are present, and rows are 1–100. Advanced-builder validation should make malformed requests fail before the first network call as well.

### Responsibility boundaries and alternatives

- A dedicated builder is preferred over adding all prompts to InteractiveISearchFlow; the flow already owns health, dataset, return-dataset, fields, results, and navigation responsibilities.
- The API client remains the only HTTP owner; the builder must not construct URLs, read User Secrets, or interpret response errors.
- Reusing the existing SearchFieldsPager for the field picker is not appropriate because the builder needs selection and filtering semantics rather than passive metadata display. Reuse its field contract and rendering conventions, but keep query-builder prompts focused.
- A raw JSON editor is intentionally excluded. In this plan, **Edit** means revisiting a typed menu setting and regenerating the preview, which preserves validation and prevents arbitrary fields or unsafe query syntax from bypassing the live schema. A literal JSON editing experience would need a separate decision about parser validation, unknown properties, and terminal/editor integration.
- Sending POST because the preview is JSON is rejected for this phase because the repository already records the body-based POST endpoint returning HTTP 500. The preview is a review representation, not an instruction to change the working transport.

## 7. Implementation steps

1. **Extend the search request contract and JSON naming.**
   - Update src/Carrot.Cli/ISearch/Contracts/SearchRequest.cs with optional QueryFields, FilterQueries, UpdatedBefore, and UpdatedAfter properties, keeping Fields as the configured fl result set.
   - Add JsonPropertyName/null-omission metadata and complete XML documentation for every changed property. Preserve the existing basic-query defaults and internal visibility.
   - Verify serialization produces the documented body-shaped names, omits unused optional values, preserves field/filter order, and does not include a cursor or credentials.

2. **Add the focused advanced-query builder.**
   - Add src/Carrot.Cli/Cli/UI/IAdvancedISearchQueryBuilder.cs and src/Carrot.Cli/Cli/UI/AdvancedISearchQueryBuilder.cs (or the nearest repository-consistent names) with constructor-injected IAnsiConsole and a method that accepts the selected database, return definition, live fields, and cancellation token.
   - Implement the help text, draft menu, field selection, repeated filter add/remove, operator choice, row-limit prompt, exact update-date parsing, and review/confirm/edit/cancel loop. Keep the return-field list from SearchReturnTypeDefinition.DefaultFields unchanged.
   - Validate selected fields against the live field names before composing qf and fq; use the returned fieldType/multiValued values to guide input without coercing unknown live types. Centralize field-value quoting/range formatting in a small private helper or focused internal formatter so it can be tested without console navigation.
   - Add the required XML documentation dividers, summaries, implementation regions, intent comments, and logical spacing for prompt validation, draft transitions, preview regeneration, and cancellation. Do not log or persist query values.

3. **Wire advanced query navigation into InteractiveISearchFlow.**
   - Add **Build Advanced Query** to the private dataset-choice model and show it only after both a live database and configured return dataset are selected. Keep the existing menu order and basic **Submit Query** action intact.
   - Inject/register the builder through src/Carrot.Cli/Composition/ServiceRegistration.cs.
   - Add an advanced-query branch that calls GetFieldsAsync for the retained database, displays safe operation messages on field-discovery failure, invokes the builder on success, and returns to the dataset menu on cancel.
   - On confirmation, reuse the existing search-result handling path. If duplication becomes necessary, extract a narrowly scoped private method for submitting a completed SearchRequest and opening SearchResultPageSession; do not alter result pager responsibilities.
   - Preserve selected database and return dataset after results, field viewing, cancel, or edit; a changed database must require fresh field discovery for the next advanced build.

4. **Extend the iSearch GET request and continuation tests.**
   - Update src/Carrot.Cli/ISearch/ISearchApiClient.cs so optional qf, fq, updatedBefore, and updatedAfter controls are encoded only when present, with existing authentication, pacing, timeout, redirect, retry, bounded-diagnostic, and cancellation logic unchanged.
   - Extend tests/Carrot.Cli.Tests/ISearch/ISearchApiClientTests.cs with exact URI assertions for omitted and populated options, spaces/quotes/range characters, array order, AND/OR, rows 1/100 and invalid 0/101, date values, and confirmation that requests remain GET and carry no credential in the URI.
   - Add a continuation case proving SearchNextPageAsync receives the same advanced request values and adds only the cursor.

5. **Test the builder and interactive use case.**
   - Add tests/Carrot.Cli.Tests/ISearch/AdvancedISearchQueryBuilderTests.cs or the nearest focused test file using TestConsole and synthetic SearchField metadata. Cover the default match-all query, qf selection, fiscal-year numeric filter, category phrase filter, multiple filters, type guidance, removal/editing, valid/invalid update dates, date-bound ordering, row boundaries, pretty JSON property names/indentation, confirm, cancel, and cancellation.
   - Extend tests/Carrot.Cli.Tests/ISearch/InteractiveISearchFlowTests.cs to prove the new menu action is hidden before return-dataset selection, field discovery is called for the selected database, the confirmed request contains the expected advanced properties, the existing result pager receives the stable request, and field/API failures return safely without search.
   - Update keyboard sequences and assertions affected by the additional menu item without weakening existing basic-query, field-discovery, result-paging, Back/Escape, and cancellation coverage.
   - If a menu extraction is needed for testability, test through the public flow and fake IISearchApiClient; do not expose private methods solely for tests.

6. **Update help and reference documentation.**
   - Update src/Carrot.Cli/Docs/isearch.md with an **Advanced query** section explaining the distinction between basic and advanced query, live field selection, q/qf/fq/fl, defaultOp, rows, updatedAfter/updatedBefore, fiscal-year/category examples, date syntax ownership, pretty JSON review, menu-based editing, and the current GET transport.
   - Update docs/cli-reference.md and any linked interactive iSearch reference text to include the new menu option and confirm that filters and dates are held in memory only. Preserve the embedded Markdown resource pattern in Carrot.Cli.csproj; do not add credentials or live payloads to docs.
   - Document that database field names/types come from GET /fields/{dataset} and can change, so examples are illustrative rather than a permanent schema.

7. **Verify the complete feature and record implementation completion.**
   - Run focused builder, API-client, interactive-flow, and existing iSearch tests first.
   - Run dotnet build Carrot-CLI.slnx --no-restore, dotnet test Carrot-CLI.slnx --no-build --no-restore, dotnet format Carrot-CLI.slnx --verify-no-changes --no-restore, and git diff --check.
   - If configured iSearch credentials are available and live verification is authorized, perform a harmless bounded interactive smoke test: select a live database and return dataset, build a fiscal-year/category or updated-date query using returned fields, inspect the pretty JSON package, choose edit once, confirm, and verify the final result request and cursor continuation. Do not print the API key, cookie, or full sensitive result payload. Otherwise report the live check as skipped; fake-handler and TestConsole tests remain the deterministic evidence.
   - After all implementation changes and checks pass, append one verified entry to C:/Source/Programs/Journal.md covering every changed source, test, configuration, and code-related documentation file. This pending plan itself is planning-only and must not be included in that future implementation entry.

## 8. Acceptance criteria

- After a live database and configured return dataset are selected, the submenu contains **Build Advanced Query**; it is absent before those prerequisites are satisfied.
- Choosing the advanced action obtains field metadata from the selected live database and presents field choices with useful type/value help. No database field catalog is hard-coded.
- The builder can create a request with a default or entered q, selected qf, zero or more fq filters, AND or OR, a positive row count no greater than 100, and valid updatedAfter/updatedBefore bounds. Fiscal-year and category filters can be expressed using live fields and documented values; date syntax is validated against the verified Zulia rules.
- The pretty-printed review package uses the documented JSON names, includes the selected database and configured fl fields, omits unused optional properties, and contains no cursor, credential, cookie, or unrelated transport metadata.
- **Edit** returns to the draft menu, preserves unchanged settings, and regenerates the exact JSON package that confirmation will submit. **Cancel**, Escape, and cancellation return safely without a search request; cancellation continues to propagate according to existing exit behavior.
- **Confirm and Submit** sends one authenticated dataset-scoped GET request with correctly encoded optional parameters. Basic requests remain compatible, and advanced values persist unchanged when the existing result pager fetches later cursor pages.
- Invalid field selections, unsupported/blank filter values, malformed dates, reversed date bounds, invalid row limits, API failures, malformed field responses, and query failures are reported safely without leaking credentials or unbounded response content.
- Returning from the advanced builder, results, or field view retains the selected database and return dataset; changing the database cannot reuse stale field metadata or query state.
- Focused tests, full build/test, formatting, whitespace checks, documentation/help verification, and any authorized live smoke test pass. New or materially changed C# follows the repository's XML documentation, divider, region, comment, naming, and spacing conventions.

## 9. Risks and assumptions

- **Editing interpretation:** This plan assumes the operator edits choices through the typed menu after viewing the JSON preview. A requirement for direct free-form JSON editing would materially change validation, parser, and terminal/editor design and should be split into a follow-up plan.
- **Filter syntax:** iSearch's fq values and field-date/range expressions are service-owned. The implementation must verify Zulia syntax from the linked authoritative documentation and live field metadata before finalizing operators, quoting, and date-range formatting; it must not rely on the illustrative examples alone.
- **Field compatibility:** A live field can be valid for discovery but unavailable for a particular query or dataset state. The API client's sanitized error remains authoritative; the builder only prevents blank/unknown selections and obvious type/input errors.
- **POST/GET divergence:** The displayed package follows the documented POST body shape, while the active client uses GET because of the repository's recorded POST failure. Documentation must make that distinction clear to avoid promising a POST request.
- **Request size:** Many filters, long phrases, or selected fields can produce a large GET URI even though the logical package is valid. The implementation should enforce a bounded URI/request size using existing safe failure conventions or document a focused limit discovered during coding.
- **Menu complexity:** Adding a full draft editor increases prompt states and keyboard-test setup. The builder boundary and state-transition tests are intended to contain that complexity without making the flow a god class.

## 10. Deferred follow-up

- Direct editing of the raw JSON package in an external editor or validated multiline terminal editor.
- Saved/reusable query templates, query history, persistence, or named command-line advanced-query options.
- Field synonym/search-variant selection, autocomplete, query-language linting, or schema caching.
- Automatic date-range presets such as “current fiscal year” after the service's fiscal-calendar semantics are explicitly defined.
- POST transport migration, arbitrary result-field editing beyond configured return datasets, and machine-readable advanced-query output.

## 11. Skill usage

- plan-software-changes: used to ground the plan in the existing flow, request contract, tests, documentation, and completed plan sequence; define one coherent implementation phase with explicit non-goals and acceptance criteria.
- isearch: used for the Build searches body shape, live /fields/{dataset} discovery, q/qf/fq/fl/date controls, AND/OR semantics, date-format caution, GET array encoding, bounded rows, cursor separation, and safe authenticated request handling.
- spectre-console-cli: used for the installed Spectre.Console 0.55.0 prompt conventions, injected-console design, cancellation propagation, literal safe output, and TestConsole-oriented interactive verification.
- dotnet-architectural-principles: used to keep prompt/draft construction, interactive flow orchestration, HTTP serialization, and result paging as separate responsibilities and to avoid expanding InteractiveISearchFlow into a god class.
- dotnet-automated-testing: used for the unit/integration split, state-transition and decision-table coverage, boundary checks for rows/dates, fake HTTP handlers, TestConsole use cases, and cancellation/error-path verification.
