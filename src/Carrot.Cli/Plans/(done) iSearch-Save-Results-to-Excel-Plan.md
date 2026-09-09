# Save Walked iSearch Results to Excel

Status: Done

## 1. Outcome

Add an explicit **Save iSearch Results to Excel** action to the interactive iSearch results view. The action writes one `.xlsx` workbook containing every result record successfully walked during the current query session, in service-page order: page 1 records followed by page 2 records, and so on. Moving through terminal display pages alone must not add or remove records; fetching a later iSearch result page must add that page to the retained export set.

The export must reuse the existing Excel persistence architecture—path suggestion and validation, overwrite confirmation, `AtomicFileWriter`, ClosedXML workbook generation, formula-safe text handling, and structured expected failures—without coupling iSearch records to the processed-document report schema.

## 2. Problem

The interactive iSearch workflow currently supports a bounded initial search and one-page-at-a-time cursor continuation, but it retains only the latest service response:

- `src/Carrot.Cli/ISearch/SearchResultPageSession.cs` exposes `CurrentPage` and replaces it after a successful `FetchNextAsync`; its documentation explicitly says it does not accumulate or persist records.
- `src/Carrot.Cli/ISearch/Contracts/SearchResponse.cs` keeps generic `JsonElement` records because iSearch datasets and fields are discovered/configured at runtime.
- `src/Carrot.Cli/Cli/UI/SearchResultsPager.cs` renders the current response and distinguishes terminal display paging from service-result paging, but it has no save action and no export boundary.
- `src/Carrot.Cli/Reporting/ExcelReportWriter.cs` writes the existing fixed 23-column processed-document `Results` worksheet from `ReportRequest`/`ReportRow`; it cannot directly represent arbitrary iSearch field sets.
- `src/Carrot.Cli/Reporting/ExcelOutputPathResolver.cs`, `ExcelOutputPathSuggester.cs`, `AtomicFileWriter.cs`, and `ProcessedResultsExportFlow.cs` already establish the repository's path, prompt, overwrite, atomic-write, and failure-feedback patterns.
- `src/Carrot.Cli/Docs/isearch.md` currently states that iSearch queries, field metadata, and results remain in memory and that no result files are written.

Without a session-level accumulation model, saving after fetching page 2 would either lose page 1 or require the UI to reconstruct discarded data. A second iSearch-specific ClosedXML implementation would also duplicate the existing Excel safety and persistence behavior.

## 3. Solution vision

The iSearch query visit will own one continuation session from the initial successful response until the operator leaves the results view. The session will retain:

- the unchanged `SearchRequest` context;
- every successfully accepted `SearchResponse` in service-page order; and
- a flattened, read-only aggregate of all records returned by those responses.

`FetchNextAsync` will remain a single network transition. After a successful fetch it will append the new page exactly once, update `CurrentPage`, and expose the aggregate for export. Failed or canceled fetches will leave both the current page and aggregate unchanged. The pager will continue to render only the current page, so display-line navigation remains presentation-only.

The save action will be available from the results pager once a successful page exists. A focused `ISearchResultsExporter`/flow will own the prompt and operation feedback, while a report mapper will convert generic JSON records plus query metadata into a reusable tabular Excel request. The low-level Excel writer will be generalized around a worksheet/table contract and continue to use `AtomicFileWriter`; the existing processed-results mapper will adapt to that contract without changing its established 23-column workbook.

The iSearch workbook will contain one `Results` worksheet. Its columns will be deterministic: selected request fields in their configured order, followed by any additional object properties observed in the walked records in first-seen order. Each record will occupy one row in aggregate service-page/record order. JSON primitives will retain useful native numeric/Boolean types where safe; nulls, arrays, and objects will be rendered as inert text (arrays/objects as deterministic JSON). All strings, including values beginning with `=`, `+`, `-`, or `@`, will be explicitly stored as Excel text.

The iSearch exporter will use the existing path suggestion/validation and overwrite interaction. It will not contact iSearch, fetch unvisited pages, reread data, create JSON sidecars, or silently truncate walked records. If the retained set exceeds an explicit safety or Excel worksheet limit, save will fail with a clear message rather than producing an incomplete workbook.

## 4. Scope

### In scope

- Accumulating all successfully walked iSearch result pages in one continuation session.
- Exposing a stable, read-only aggregate suitable for export without changing current-page rendering.
- Adding an explicit save action to the iSearch results pager and retaining the query session after save success, decline, or expected failure.
- Reusing the existing Excel path prompt, overwrite confirmation, atomic sibling-file promotion, ClosedXML dependency, formatting conventions, and formula-injection protections.
- Generalizing the low-level Excel table/workbook request as needed so processed-document and iSearch exports share one writer/persistence path.
- Mapping generic iSearch JSON fields and values into deterministic worksheet columns and rows, including page/record provenance if confirmed in the open decisions below.
- Focused automated tests for aggregation, page transitions, export mapping, generic Excel writing, pager interaction, composition, and documentation.
- Updating iSearch help and output documentation to describe explicit save scope, privacy, limits, and page aggregation.

## 5. Non-goals

- Automatically walking all iSearch result pages.
- Saving a page that has not yet been fetched by the operator.
- Changing iSearch authentication, field discovery, query syntax, cursor protocol, retry policy, or request pacing except where accumulation limits require a validated local guard.
- Adding a background crawler, resume checkpoint, scheduler, database, CSV/JSON export, or cloud upload.
- Deduplicating records across pages without an explicit stable identifier and confirmed product decision.
- Changing the existing processed-document workbook's 23-column contract, row semantics, or user-facing save workflow beyond the internal writer abstraction needed for reuse.
- Adding formulas, external links, macros, multiple worksheets, charts, or automatic output directories.

## 6. Technical approach

### Session accumulation and invariants

Extend `SearchResultPageSession` with an immutable/read-only `WalkedResults` collection and, if needed, a `WalkedPages`/page metadata collection. Initialize it from the first successful page. On each successful `FetchNextAsync`, append the returned records after the existing aggregate and only then replace `CurrentPage`. A failed operation, cancellation, or unavailable cursor must not mutate the aggregate.

Preserve the existing cursor and cardinality invariants: continuation requests use the unchanged `SearchRequest`, the current cursor, and the next service page number; display-page changes never touch the aggregate. The session must prevent accidental double-append if a caller retries or re-enters a completed transition. The aggregate should be exposed as `IReadOnlyList<JsonElement>` or an equivalent immutable value so exporters cannot mutate session state.

The session should track enough provenance to make the workbook auditable. The recommended default is to add `ResultPage` and `ResultOrdinal` columns before the dynamic iSearch fields. These columns make it possible to verify that page 1 and page 2 were combined in the intended order and distinguish repeated records without pretending that the client has a universal dataset identifier.

### Reusable Excel architecture

Extract a generic internal table model from the current processed-results-specific request, for example a workbook request containing:

- destination path and overwrite policy;
- worksheet name;
- ordered column definitions;
- ordered rows of typed cell values; and
- the formatting hints needed for wrapped/long-text columns.

Keep `ReportRequest`/`ReportRow` as the processed-results mapping vocabulary if that minimizes churn, but adapt it to the generic model at the Excel boundary. The final names should follow the repository's established naming style. `IExcelReportWriter` may become a more general `IExcelWorkbookWriter` if that produces a clean contract; avoid adding a second ClosedXML writer that duplicates atomic persistence, header formatting, native-value handling, and string safety.

The shared writer must continue to:

- validate request arguments before writing;
- write through `AtomicFileWriter` in the destination directory;
- create exactly one `Results` worksheet for both current export types;
- freeze and filter the header row;
- use bounded widths and wrapping for long fields;
- store all untrusted strings as explicit text; and
- preserve an existing destination when generation fails, cancellation occurs, or overwrite was not approved.

The processed-results tests must continue to assert the existing headers and values after the abstraction is generalized. iSearch-specific tests must assert dynamic headers, row order, native primitive types, JSON serialization, formula-looking values, and atomic failure behavior.

### iSearch mapping

Add a focused mapper under `src/Carrot.Cli/Reporting/` or `src/Carrot.Cli/ISearch/` that receives the stable `SearchRequest`, walked pages/results, and output path policy. Derive columns from configured request fields first, then append properties found in records but absent from the request field list. Handle non-object records explicitly: either map a single `Value` column or return a structured unsupported-record failure; choose one behavior and test it rather than dropping data.

For each walked record, preserve service-page order and include page/ordinal provenance if the recommended metadata columns are accepted. Map JSON values as follows:

- strings: explicit Excel text;
- integers and floating-point numbers: native numeric cells when representable without loss;
- `true`/`false`: native Boolean cells;
- `null`: blank text/value according to the generic cell contract;
- arrays and objects: deterministic compact JSON text stored as explicit text; and
- unsupported or excessively large values: a safe, actionable export failure before final-file promotion.

Use ordinal metadata rather than a fabricated identifier. If deduplication is later desired, it should be a separate, documented policy based on a configured live field such as `id`; it must not be inferred from JSON shape.

### Interactive flow

Add `Save iSearch Results to Excel` to `SearchResultsPager.PageChoice` after at least one successful page. The pager should pass the same session to the flow and return to the same current service/display page after a completed save or expected failure. Save must not fetch another page, alter the selected database/return dataset, or clear the accumulated records.

The flow should mirror `ProcessedResultsExportFlow`: suggest a complete timestamped `.xlsx` path, accept quoted or unquoted paths through `ExcelOutputPathResolver`, ask before replacing an existing file, invoke the iSearch exporter, display the normalized saved path, and retain the in-memory session. Cancellation continues to propagate to the owning interactive menu.

### Limits and privacy

Do not make the save action an implicit full-dataset export. The workbook contains only pages explicitly fetched during the current visit. Add a validated local maximum for retained/exportable records or pages only if needed to avoid unbounded memory and to remain below Excel's worksheet row limit; expose the limit and failure behavior in configuration/help. The exporter must refuse to write a partial subset when the limit is exceeded.

The workbook can contain query text, dataset names, selected fields, and returned research data. Document that saving is explicit, local, and user-directed; no credentials are written; no sidecar or log is created by this action; and leaving the result view discards the in-memory walk.

### Alternatives considered

- **Save only `CurrentPage`:** rejected because it loses earlier pages and violates the requested page-1-plus-page-2 behavior.
- **Accumulate records inside `SearchResultsPager`:** rejected because UI state would own data correctness and future non-UI consumers would need to duplicate aggregation.
- **Automatically fetch all pages when Save is selected:** rejected because it changes an explicit bounded interactive action into an unbounded network operation and conflicts with the existing cursor/pacing boundary.
- **Create a second iSearch-specific ClosedXML writer:** rejected because it duplicates the existing atomic write, overwrite, formatting, and formula-safety architecture.
- **Force iSearch records into `ReportRow`:** rejected because that would corrupt the processed-document contract and discard arbitrary dataset fields.
- **Use a fixed hard-coded iSearch schema:** rejected because datasets and return fields are configurable/live and records are intentionally modeled as generic JSON.

## 7. Implementation steps

1. **Confirm product decisions and the generic workbook contract.**
   - Files/symbols: this plan; `src/Carrot.Cli/Reporting/ReportRequest.cs`, `ReportRow.cs`, `IExcelReportWriter.cs`, `ExcelReportWriter.cs`; existing output documentation.
   - Change: settle metadata columns, duplicate handling, non-object JSON behavior, and the retained-record/page safety limit. Define the smallest generic typed-cell/table contract that supports both processed-document and iSearch rows.
   - Why: these choices determine workbook compatibility and whether the current writer can be generalized without leaking one report's schema into another.
   - Verify: document the decisions in the implementation plan/issue before changing the contract; preserve the existing processed-results column order.

2. **Extend the iSearch session to retain the complete walked set.**
   - Files/symbols: `src/Carrot.Cli/ISearch/SearchResultPageSession.cs`, `src/Carrot.Cli/ISearch/Contracts/SearchResponse.cs` only if contract documentation needs clarification, and `tests/Carrot.Cli.Tests/ISearch/SearchResultPageSessionTests.cs`.
   - Change: initialize aggregate state from page 1, append each successful later page in order, expose read-only walked records/page provenance, and keep state unchanged on failure, cancellation, unavailable continuation, or repeated transition attempts.
   - Why: aggregation belongs beside cursor/page transition state and must be independent of terminal rendering.
   - Verify: assert page 1 only, page 1 plus page 2, page 1 plus pages 2–3, failed fetch retention, canceled fetch retention, terminal cursor behavior, exact append order, and no duplicate append from a failed/retried operation.

3. **Generalize the shared Excel persistence seam.**
   - Files/symbols: `src/Carrot.Cli/Reporting/IExcelReportWriter.cs`, `ExcelReportWriter.cs`, `ReportRequest.cs`, `ReportRow.cs`, `src/Carrot.Cli/Common/AtomicFileWriter.cs` only if the generalized callback needs a contract adjustment; existing `tests/Carrot.Cli.Tests/Reporting/ExcelReportWriterTests.cs`, `AtomicFileWriterTests.cs`, and processed-results reporting tests.
   - Change: extract/reuse a generic worksheet request and typed cell representation while preserving the existing processed-results adapter, worksheet name, 23 headers, formatting, formula-safe strings, overwrite behavior, and atomic promotion.
   - Why: both export types need one implementation of the risky workbook/persistence behavior.
   - Verify: run existing processed-results workbook tests unchanged or with focused fixture updates; add coverage for dynamic columns, typed cells, long text, formula-looking strings, cancellation, overwrite refusal, and failed generation preserving the prior destination.

4. **Implement deterministic iSearch record mapping.**
   - Files/symbols: new mapper/value-model files under `src/Carrot.Cli/Reporting/` (or `src/Carrot.Cli/ISearch/` if that better preserves ownership); `tests/Carrot.Cli.Tests/Reporting/` or `tests/Carrot.Cli.Tests/ISearch/` focused mapper tests.
   - Change: map the session's walked records into ordered dynamic columns and typed cells, preserve configured field order plus first-seen extras, serialize nested values deterministically, and add page/ordinal metadata if selected.
   - Why: this isolates generic JSON interpretation from ClosedXML and proves that no walked record or returned field is silently discarded.
   - Verify: cover multiple pages, empty pages, missing fields, extra fields, nulls, strings/numbers/Booleans, arrays/objects, non-object records, duplicate records, formula-like values, property order variation, and large/unsupported values.

5. **Add iSearch export orchestration and prompt flow.**
   - Files/symbols: new `ISearchResultsExporter`/implementation and `ISearchResultsExportFlow`/implementation (final names to match current `IProcessedResultsExporter`/`ProcessedResultsExportFlow` conventions); reuse `ExcelOutputPathResolver.cs` and `ExcelOutputPathSuggester.cs`; `tests/Carrot.Cli.Tests/Reporting/` and `tests/Carrot.Cli.Tests/Cli/` focused tests.
   - Change: validate the session and path, pass overwrite intent to the shared writer, translate expected filesystem/workbook failures into `OperationResult` messages, and provide new-file, overwrite-decline, overwrite-accept, invalid-path, failure, and cancellation interactions.
   - Why: export policy and terminal prompting belong outside the page session and workbook implementation and should match processed-results behavior.
   - Verify: assert no API call occurs during save, the exact accumulated session reaches the mapper, the saved path is normalized, the current session survives every expected outcome, and cancellation is not converted into an ordinary failure.

6. **Integrate Save into results navigation.**
   - Files/symbols: `src/Carrot.Cli/Cli/UI/SearchResultsPager.cs`, `ISearchResultsPager.cs` only if its contract changes, `src/Carrot.Cli/Cli/UI/InteractiveISearchFlow.cs` only where the session is handed through, and `tests/Carrot.Cli.Tests/ISearch/SearchResultsPagerTests.cs`, `InteractiveISearchFlowTests.cs`.
   - Change: add the conditional save choice, invoke the flow with the current session, preserve separate display/result paging, and return to the same result page after save.
   - Why: this exposes the feature at the point where the operator has walked one or more pages without making the pager responsible for Excel details.
   - Verify: prove save is absent before a successful search, present after page 1, still present after page 2, includes both pages, does not appear as a result-page fetch, does not re-query, and does not alter dataset/return-dataset selections or current display navigation.

7. **Register the export graph and preserve composition boundaries.**
   - Files/symbols: `src/Carrot.Cli/Composition/ServiceRegistration.cs`; `tests/Carrot.Cli.Tests/Cli/CommandRouteTests.cs` and any focused composition test.
   - Change: register the iSearch exporter/flow and any shared workbook abstractions with lifetimes consistent with existing stateless writers and transient UI orchestration. Do not duplicate `AtomicFileWriter`, path resolvers, or ClosedXML registrations.
   - Why: the interactive menu must resolve the new graph while existing processed-results export remains operational.
   - Verify: resolve the results pager and export graph from the production service collection and assert existing processed-results services still resolve.

8. **Update operator documentation and help.**
   - Files/symbols: `src/Carrot.Cli/Docs/isearch.md`, `src/Carrot.Cli/Docs/output-columns.md` if the shared output contract needs a new section, `docs/cli-reference.md`, `README.md`, `docs/output-format.md`, and relevant troubleshooting/privacy/help tests.
   - Change: describe explicit Save iSearch Results to Excel, page aggregation, service-page versus display-page behavior, dynamic columns/metadata, limits, formula safety, overwrite/atomic behavior, local data sensitivity, and the fact that only visited pages are saved.
   - Why: current iSearch documentation explicitly says no result files are written, and users need to understand that Save does not fetch more data.
   - Verify: embedded help/resource tests pass, stale “iSearch results are never written” claims are removed, and shared processed-results documentation remains accurate.

9. **Run focused and full verification.**
   - Run focused iSearch session, mapper, Excel writer, export-flow, pager, and composition tests first.
   - Run `dotnet build .\\Carrot-CLI.slnx --no-restore --disable-build-servers -m:1 --verbosity minimal`, `dotnet test .\\Carrot-CLI.slnx --no-build --no-restore --verbosity normal`, `dotnet format .\\Carrot-CLI.slnx --verify-no-changes --no-restore` when applicable, and `git diff --check`.
   - Confirm `src/Carrot.Cli/Carrot.Cli.csproj` continues to emit XML documentation and documentation-convention tests cover every materially modified C# member.
   - Perform a bounded manual smoke test only when iSearch User Secrets are present: submit one query, save page 1, fetch page 2, save again, and inspect that the second workbook contains page-1 rows followed by page-2 rows. Do not log credentials or response data unnecessarily.
   - After implementation, the coordinating agent must append one verified journal entry covering every changed code, test, configuration, and code-related documentation file to `C:\Source\Programs\Journal.md`.

## 8. Acceptance criteria

- The interactive iSearch results view offers **Save iSearch Results to Excel** after a successful response, including an empty response, and does not offer it before a query has completed.
- Saving after only page 1 creates a valid `.xlsx` workbook containing page 1's records.
- Fetching page 2 and saving creates a workbook containing all page-1 records followed by all page-2 records; fetching additional pages extends the same ordered aggregate.
- Terminal **Next Display Page** and **Previous Display Page** actions never change the saved record set or issue an API request.
- Failed/canceled page fetches do not change the aggregate, and save never fetches unvisited pages.
- The workbook's columns are deterministic and include every returned field represented by the walked records, with configured selected fields first and any observed extras handled by the agreed policy.
- Page/record provenance columns are included if accepted in the product decision; otherwise the workbook still preserves unambiguous service-page/record order through tested row ordering.
- Strings are inert Excel text, numeric/Boolean primitives retain native types where supported, and nested values have deterministic text representations.
- Existing `.xlsx` destinations require explicit overwrite approval; decline leaves the destination unchanged. Workbook creation is atomic and failures/cancellation do not leave a partial final file or orphan temporary file.
- The processed-document Excel export retains its current 23-column output contract and behavior after the writer abstraction is generalized.
- Save success, expected failure, overwrite decline, and cancellation retain the active iSearch session and return to the correct interactive context.
- Documentation accurately states that the export is explicit, local, limited to pages walked in the current visit, and does not write credentials, sidecars, or logs.
- Focused tests, full build/test/format checks, and whitespace verification pass, with any live-service check limitation reported.

## 9. Risks and assumptions

- The current API intentionally models records as generic JSON. The plan assumes iSearch result records are normally JSON objects, but it requires an explicit tested behavior for a scalar/array root record rather than silently dropping it.
- The request's `Fields` list is the primary column order, but the plan assumes the service may return additional object properties. The mapper must preserve those extras or report them; it must not discard them silently.
- Excel has finite worksheet rows and cell lengths, while manual page walking can accumulate many records. The implementation must establish a configured or hard Excel-safe bound before writing and must fail rather than truncate. The exact default limit is an open product decision.
- Cursor paging is expected to produce disjoint ordered pages. Until a stable identifier policy is chosen, the default should preserve every returned row in service order and not deduplicate.
- The iSearch workbook's metadata shape is not yet specified. The recommended shape adds `ResultPage` and `ResultOrdinal`; dataset/query/return-dataset context can remain workbook metadata or become additional columns based on the decision below.
- No live iSearch call is required to create or review this plan. Any implementation smoke test must use the configured User Secrets key/contact address, honor the authenticated rate limit, and avoid exposing credentials.

## 10. Resolved implementation decisions

The implementation uses the recommended defaults below:

1. Should the workbook include `ResultPage` and `ResultOrdinal` columns? Recommended: yes, because they make the combined page walk auditable without relying on a dataset-specific ID.
2. Should duplicate records across pages be preserved or deduplicated? Recommended: preserve them exactly as returned until a stable identifier field and deduplication rule are explicitly selected.
3. For root JSON values that are not objects, should the exporter use a single `Value` column or reject the export? Recommended: use a `Value` column so no walked data is discarded.
4. What maximum number of accumulated records/pages should the interactive session/export allow? The implementation enforces Excel's 1,048,575 data-row capacity and fails clearly before mutating the retained walk when the next page would exceed it.
5. Should query context (`Dataset`, query text, configured return-dataset name) be workbook columns, a small metadata block above the table, or omitted? The implementation keeps the workbook focused on walked result data and provenance; query context remains in the active in-memory session and is not written to the worksheet.

## 11. Deferred follow-up

- Automatic full-dataset crawling and export with a user-selected maximum page count.
- Resumable export/checkpointing across CLI sessions.
- Cross-query append/merge, saved-search catalogs, or deduplication based on dataset-specific identifiers.
- Multiple worksheets, dashboards, charts, or a separate workbook metadata sheet.
- Schema validation of configured return fields against live `/fields/{dataset}` before every save.

## 12. Skill usage

- `plan-software-changes`: used to ground the plan in current iSearch/session/pager/reporting symbols, define one coherent implementation phase, and specify actionable verification.
- `isearch`: used for cursor continuation, generic response-field handling, bounded walking, authenticated request pacing, and safe live-service verification constraints.
- The existing repository Excel architecture was used as the design boundary: `ExcelOutputPathResolver`, `ExcelOutputPathSuggester`, `AtomicFileWriter`, `IExcelReportWriter`, `ExcelReportWriter`, and `ProcessedResultsExportFlow`.
