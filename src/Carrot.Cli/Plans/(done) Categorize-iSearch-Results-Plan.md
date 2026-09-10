# Categorize iSearch Results

Status: Completed

## 1. Outcome

Add an interactive **Categorize iSearch Results** action to the existing iSearch results menu. When selected, the action submits the records already retained by the current `SearchResultPageSession` to Carrot for categorization, preserving the walked record order and submitting only these fields for each record:

- `nihApplId`
- `title`
- `abstract`
- `specificAims`

The action must reuse the existing Carrot `/list` validation, `/cluster` submission, response-index correlation, cancellation, retry, timeout, and safe-failure pathways created for document/file categorization. It must not fetch additional iSearch pages implicitly. After a successful categorization, show a categorized-results view with a **Save Categorized iSearch Results to Excel** action that writes the retained iSearch fields and Carrot category memberships through the existing shared atomic Excel writer.

## 2. Problem

The completed iSearch page-walking and raw Excel-export work provides the data and persistence foundations, but it does not connect iSearch records to Carrot categorization:

- `src/Carrot.Cli/ISearch/SearchResultPageSession.cs` retains `WalkedPages` and `WalkedResults` in service-page order, but it has no categorization operation.
- `src/Carrot.Cli/Cli/UI/SearchResultsPager.cs` offers display paging, one-page continuation, **Fetch All Pages**, and raw **Save iSearch Results to Excel**, but no Carrot categorization action.
- `src/Carrot.Cli/ISearch/Contracts/SearchResponse.cs` intentionally keeps records as generic `JsonElement` values because iSearch schemas are discovered/configured at runtime.
- The document/file path already validates Carrot configuration, creates an ordered `ClusterRequest`, submits one request through `ICarrotApiClient`, flattens recursive memberships with `ClusterMembershipMapper`, and correlates response indexes in `PreparedDocumentProcessor`.
- `src/Carrot.Cli/Reporting/ExcelReportWriter.cs` already accepts the generic `ExcelWorkbookRequest` contract and writes typed cells through `AtomicFileWriter`; `SearchResultsReportMapper` demonstrates dynamic iSearch field mapping, while `ProcessedDocumentReportMapper` demonstrates category-membership workbook rows.

Without an adapter, the iSearch pager would either duplicate the Carrot request/validation/correlation code or incorrectly force research records into the file-oriented `ExtractedDocument` and 23-column `ReportRow` models. Without a category-specific report model, exporting would either lose the requested iSearch fields or produce a workbook dominated by irrelevant file metadata.

## 3. Solution vision

Keep the current iSearch session as the sole owner of loaded query records and keep the Carrot API client as the sole owner of Carrot HTTP behavior. Add a shared, source-neutral Carrot categorization boundary by extracting the common `/list` validation, `/cluster` submission, membership mapping, and successful-run metadata from `PreparedDocumentProcessor`. The existing document workflow will continue to use that boundary and will retain its current `ProcessedDocumentBatch` output.

The iSearch path will map the session's `WalkedResults` into an ordered Carrot request without rereading or transforming unrelated fields. Each accepted result must be a JSON object. The adapter will emit exactly those four peer document fields; an absent selected property is retained as a source null and emitted as an empty Carrot string. It will not add a fabricated `content` field, query metadata, local paths, credentials, or terminal-display data. Because Carrot document values must be strings or arrays of strings, numeric, missing, and null scalar values are normalized to strings at the service boundary while original values remain in the categorized rows and workbook. Incompatible structured values produce a safe preflight failure before Carrot is contacted.

The control and data flow will be:

```text
InteractiveISearchFlow
    -> SearchResultsPager
        -> Categorize iSearch Results
            -> iSearch categorization flow
                -> iSearch record/request adapter
                    -> shared Carrot categorization service
                        -> GET /list -> POST /cluster
                        -> ClusterMembershipMapper
                    -> categorized iSearch batch
                -> categorized-results pager
                    -> categorized Excel export flow
                        -> shared ExcelWorkbookRequest
                            -> ExcelReportWriter -> AtomicFileWriter
```

The categorization action consumes exactly the records currently in `SearchResultPageSession.WalkedResults`. If the operator wants the complete query result set, they use **Fetch All Pages** first. A partial walk may be categorized deliberately, but the categorized view and workbook must report the loaded count and must not imply that unvisited iSearch pages were included.

The categorized workbook will contain one `Results` worksheet with result provenance, the four submitted iSearch fields, and category information. Assigned records produce one row per flattened Carrot membership in source/result order; unassigned records produce one row with blank category values. This follows the established processed-document report semantics while keeping iSearch data in an iSearch-specific mapper. The low-level typed-cell, formula-safe string, overwrite-confirmation, atomic-write, and temporary-file cleanup behavior remains shared.

## 4. Scope

- Extract the reusable Carrot categorization operation from the file-oriented processor without changing the existing document-processing contract or behavior.
- Add an iSearch-to-Carrot request adapter for exactly `nihApplId`, `title`, `abstract`, and `specificAims`.
- Add a categorized iSearch result model that retains the original selected fields, service-page/result order, Carrot run metadata, and all correlated memberships.
- Add **Categorize iSearch Results** to the current iSearch results menu and keep it separate from terminal display paging, iSearch page fetching, and raw iSearch export.
- Add a categorized-results display and an explicit **Save Categorized iSearch Results to Excel** action.
- Reuse `EndpointResolver`, the existing default Carrot endpoint behavior, clustering selection defaults, `ICarrotApiClient`, `ClusterMembershipMapper`, `ExcelOutputPathResolver`, `ExcelOutputPathSuggester`, `ExcelReportWriter`, and `AtomicFileWriter`.
- Factor any duplicated endpoint/export interaction needed by the new flow into focused shared helpers while preserving current document/file menu behavior and wording where it is already part of the user contract.
- Add focused unit, integration-style fake-client, pager, export, composition, and documentation coverage.

## 5. Non-goals

- Do not fetch unvisited iSearch pages when categorization or categorized export is selected.
- Do not change iSearch authentication, query syntax, return-dataset discovery, cursor semantics, request pacing, retry policy, timeout policy, or raw **Save iSearch Results to Excel** behavior.
- Do not submit fields other than the four named iSearch fields, including all other configured return fields, query text, dataset names, local paths, hashes, or terminal metadata.
- Do not split the Carrot request into one request per record or multiple batches; preserve one ordered clustering request so Carrot's global clustering semantics remain unchanged.
- Do not force iSearch records into `ExtractedDocument`, `PreparedDocumentBatch`, `ProcessedDocumentRow`, or the existing 23-column file-oriented report schema.
- Do not add automatic background categorization, persistent categorized sessions, scheduled execution, cross-query merging, deduplication, or a new noninteractive iSearch command.
- Do not add a second ClosedXML writer, a second atomic persistence implementation, or a second Carrot HTTP client.
- Do not change the existing processed-document workbook's columns, row semantics, menu behavior, or export safety except for the internal shared abstraction changes required to remove duplication.
- Do not silently omit malformed records, selected fields, unassigned records, duplicate records, or overlapping/nested memberships.

## 6. Technical approach

### Shared Carrot categorization boundary

Introduce a source-neutral internal request/result contract, with final names matching repository conventions, such as `CarrotCategorizationRequest`, `CarrotCategorizationResult`, and `ICarrotCategorizer`. The request should contain the normalized endpoint, a complete ordered `ClusterRequest`, `ClusteringSelection`, and timeout. The result should contain the successful run identifier, effective configuration/template, exact request, exact response, and memberships grouped by the zero-based Carrot document index.

Move the common sequence currently implemented by `PreparedDocumentProcessor` into the shared categorizer:

1. Resolve defaults, template precedence, and optional parameter files through `ClusteringConfigurationResolver`.
2. Retrieve and validate the exact Carrot configuration through `ICarrotApiClient.GetConfigurationAsync` and `ClusteringConfigurationValidator`.
3. Submit one ordered request through `ICarrotApiClient.ClusterAsync`.
4. Validate and flatten all response indexes through `ClusterMembershipMapper`.
5. Assign the run identifier only after complete success.

`PreparedDocumentProcessor` will remain the file-workflow adapter: it uses the existing `ClusterRequestFactory` overload for `ExtractedDocument` values, invokes the shared categorizer, and projects memberships back into the existing `ProcessedDocumentBatch` and `ProcessedDocumentRow` models. The shared request factory may be generalized with an overload for already-created `ClusterDocument` values, but the established title/content mapping for local documents must remain unchanged.

Expected operation failures remain `OperationResult` messages; caller cancellation remains an `OperationCanceledException`. No layer above the shared categorizer should recreate `/list` validation, endpoint URI construction, retries, response-index mapping, or run-ID policy.

### iSearch record mapping and correlation

Add an iSearch categorization adapter under `src/Carrot.Cli/ISearch/` or `src/Carrot.Cli/Processing/` that reads only `SearchResultPageSession.WalkedPages`/`WalkedResults`. It must:

- Enumerate records in page order and preserve duplicate records exactly as returned.
- Require each record to be a JSON object and inspect `nihApplId`, `title`, `abstract`, and `specificAims` when present; absent selected properties are tolerated as source nulls.
- Accept the documented scalar forms needed by the dataset, preserve values as cloned `JsonElement` values, and retain explicit nulls. Unsupported structured values must produce a structured mapping failure that identifies the result ordinal without echoing unbounded record content.
- Create one `ClusterDocument` per record with exactly the four selected wire properties and no local/session metadata. Verify serialization so extension-data property names do not collide with the existing `title`/`content` properties.
- Keep an explicit map from Carrot request index to the originating iSearch result ordinal/page. Response indexes are never matched by `nihApplId`, because the Carrot contract defines them as positions in the exact submitted array.
- Create a categorized batch containing the four retained values, `ResultPage`, `ResultOrdinal`, Carrot run metadata, and a read-only membership list for each submitted record.

The adapter must fail before `/list` or `/cluster` when loaded records contain unsupported structured values, but missing selected properties remain valid empty request fields. A valid Carrot response with no memberships remains a successful categorization with every record marked unassigned.

### Categorized display and menu flow

Extend `SearchResultsPager.PageChoice` with **Categorize iSearch Results**, placed after service-page actions and before raw export/back navigation. Show it when at least one record is retained; do not derive availability from terminal display-page count. The current live-display ownership rules require the pager to leave its `LiveDisplay` before endpoint prompts or another result pager starts, then restore the original iSearch result view after categorized navigation ends.

Use a focused `ISearchResultsCategorizationFlow`/implementation to prompt for and validate the Carrot endpoint with the existing `EndpointResolver` behavior, invoke the iSearch adapter/shared categorizer, render safe diagnostics, and return the categorized batch. If endpoint prompting is currently embedded in `ProcessDocumentsMenu`, extract that prompt/validation responsibility so both workflows share the same default and validation rules.

On success, open a categorized-results pager that displays one iSearch record at a time or in the established bounded page size, including `nihApplId`, title, assignment state, membership count, category paths, and scores. Keep category paths and scores in depth-first server order, preserve overlaps and nested memberships, and render all untrusted values as literal text. The pager must offer **Save Categorized iSearch Results to Excel**, return to the categorized view after a save/decline/expected failure, and return to the iSearch result view on Back or Escape.

### Categorized Excel export

Add an iSearch-category report mapper/export boundary that targets the existing `ExcelWorkbookRequest`/`IExcelWorkbookWriter` contract rather than `ReportRequest`/`ReportRow`. The recommended deterministic columns are:

1. `ResultPage`
2. `ResultOrdinal`
3. `nihApplId`
4. `title`
5. `abstract`
6. `specificAims`
7. `CategoryCount`
8. `CategoryPaths`
9. `CategoryScores`
10. `CategoryMembershipsJson`

Use one row per membership, repeating the four iSearch fields and provenance for assigned records. Use one row with blank category columns for an unassigned record. Serialize complete membership objects deterministically and use invariant, unrounded score text consistent with `ProcessedDocumentReportMapper`. Store all source strings and JSON as explicit Excel text; use native numeric/Boolean cells only when the value contract makes that safe.

The export flow should share the existing path suggestion/validation, overwrite confirmation, cancellation propagation, and safe feedback. If the existing processed and raw iSearch export flows duplicate those prompts, factor the common interaction into a small reusable helper with caller-provided operation text; do not duplicate path or overwrite policy. The exporter must not call iSearch or Carrot and must not fetch pages. It writes only the categorized batch captured by the completed categorization call.

### Alternatives considered

- **Duplicate `PreparedDocumentProcessor` for iSearch:** rejected because `/list` validation, Carrot request execution, retry/timeout behavior, run identity, and membership correlation would drift between file and iSearch workflows.
- **Wrap iSearch records in synthetic `SourceFile`/`ExtractedDocument` objects:** rejected because it invents file metadata, obscures the four-field wire contract, and couples research records to file-report columns.
- **Categorize only `CurrentPage`:** rejected because it silently loses pages already loaded through **Fetch Next Result Page** or **Fetch All Pages**.
- **Fetch all remaining pages automatically when categorization starts:** rejected because the requested operation is over loaded data and the existing explicit page-walk action already controls network traversal and throttling.
- **Match Carrot response memberships by `nihApplId`:** rejected because the documented Carrot response uses zero-based request indexes and identifiers may be missing, duplicated, or typed differently.
- **Create a second category-specific Excel writer:** rejected because the generic typed-cell writer and atomic persistence boundary already cover the required workbook safety behavior.
- **Reuse the 23-column processed-document report directly:** rejected because its source paths, extraction status, hashes, and file metadata do not describe iSearch records and would make the output contract misleading.

## 7. Implementation steps

1. **Define the shared categorization contracts and extract common Carrot execution.**
   - Files/symbols: new internal contracts under `src/Carrot.Cli/CarrotApi/` or `src/Carrot.Cli/Processing/`; `src/Carrot.Cli/Processing/PreparedDocumentProcessor.cs`; `src/Carrot.Cli/CarrotApi/ClusterRequestFactory.cs`; `src/Carrot.Cli/CarrotApi/ClusterMembershipMapper.cs`; `src/Carrot.Cli/CarrotApi/ICarrotApiClient.cs` only if documentation requires it.
   - Change: move `/list` validation, `/cluster` submission, response mapping, and successful run metadata into one shared categorizer; preserve the existing `IPreparedDocumentProcessor` boundary and document request mapping through an adapter.
   - Why: both local documents and iSearch records must use one Carrot execution path and one set of failure/cancellation invariants.
   - Verify: existing prepared-document processor/API tests continue to assert exact request order, configuration validation, response correlation, empty clusters, invalid indexes, cancellation, retry, timeout, and failure retention.

2. **Implement exact four-field iSearch-to-Carrot mapping.**
   - Files/symbols: new iSearch categorized-batch/request-adapter contracts; `src/Carrot.Cli/ISearch/Contracts/SearchResponse.cs` only if a cloned-value contract needs documentation; `src/Carrot.Cli/CarrotApi/Contracts/ClusterDocument.cs`/`ClusterRequestFactory.cs` if the generic document-field overload is required; new `tests/Carrot.Cli.Tests/ISearch/SearchResultsCategorizerTests.cs` and focused request-factory tests.
   - Change: map `WalkedPages` in service order, validate record object shape and supported value shapes, normalize Carrot-incompatible scalar values at the wire boundary while preserving source values in the result model, emit exactly the four requested document properties, and retain page/ordinal-to-Carrot-index correlation.
   - Why: generic iSearch JSON cannot be passed to the file processor without losing the source contract or adding false file metadata.
   - Verify: assert exact serialized request JSON, field order where the serializer contract allows it, no `content`/path/hash/query properties, duplicate and out-of-order source data preservation, missing-field tolerance before HTTP, scalar/null behavior, unsupported-value failure, and Carrot-index correlation with out-of-order/nested memberships.

3. **Add the iSearch categorization operation and endpoint flow.**
   - Files/symbols: new `ISearchResultsCategorizationFlow` and implementation under `src/Carrot.Cli/Cli/UI/`; new iSearch categorizer service; `src/Carrot.Cli/Cli/UI/ProcessDocumentsMenu.cs` if endpoint prompting is extracted; `src/Carrot.Cli/Cli/UI/SearchResultsPager.cs`.
   - Change: add the exact **Categorize iSearch Results** action, submit only the retained session data, use the shared Carrot endpoint/configuration path, leave the live result display before prompts, show safe failures, and open the categorized-results view on success. Restore the original iSearch results view without replacing or clearing its session.
   - Why: the pager should coordinate navigation while focused flows own prompts and source-specific orchestration.
   - Verify: `SearchResultsPagerTests` cover menu ordering/visibility, initial-page and all-pages data sets, no implicit iSearch fetch, exact session forwarding, endpoint validation, successful categorized navigation, failure/cancellation, Escape/Back, and preservation of the raw export action.

4. **Create the reusable categorized-results presentation path.**
   - Files/symbols: new categorized result view model/pager under `src/Carrot.Cli/Cli/UI/`; existing `src/Carrot.Cli/Cli/UI/ProcessedResultsPager.cs` if common category navigation is extracted; new focused pager tests plus updates to `tests/Carrot.Cli.Tests/Cli/ProcessDocumentsMenuTests.cs`.
   - Change: share bounded category-row rendering, assigned/unassigned status, category path/score formatting, Escape/Back behavior, and save-action placement between file categorization and iSearch categorization. Keep the existing processed-document display and menu contract unchanged.
   - Why: category navigation has the same membership semantics in both workflows, while source-field projections differ.
   - Verify: existing processed pager tests remain green; iSearch pager tests assert requested fields, page navigation, overlapping/nested memberships, empty cluster results, literal rendering, and return to the originating iSearch view.

5. **Add categorized iSearch Excel mapping and export.**
   - Files/symbols: new categorized iSearch report model/mapper/exporter/request under `src/Carrot.Cli/Reporting/`; `src/Carrot.Cli/Reporting/ExcelReportWriter.cs`, `IExcelWorkbookWriter.cs`, `ExcelWorkbookRequest.cs` only if the generic contract needs a focused extension; reuse `ExcelOutputPathResolver.cs`, `ExcelOutputPathSuggester.cs`, and `AtomicFileWriter.cs`; new reporting/export-flow tests.
   - Change: map the exact ten-column categorized worksheet contract, expand one record into one row per membership or one unassigned row, preserve result page/ordinal order, use deterministic membership JSON and invariant scores, and invoke the shared atomic writer and path/overwrite flow.
   - Why: the workbook must preserve the requested iSearch data and categories without duplicating ClosedXML or filesystem safety code.
   - Verify: reopen generated workbooks and assert exact headers/order, rows/membership expansion, unassigned records, duplicate records, null/scalar values, formula-looking strings, long text, native-safe types, overwrite refusal/acceptance, cancellation, no API calls during save, and no orphan temporary files.

6. **Register the complete graph and preserve existing composition.**
   - Files/symbols: `src/Carrot.Cli/Composition/ServiceRegistration.cs`; `tests/Carrot.Cli.Tests/Cli/CommandRouteTests.cs` or a focused composition test.
   - Change: register the shared categorizer, iSearch adapter/batch, categorization flow, categorized pager, mapper, exporter, and any extracted shared endpoint/export helpers with lifetimes matching existing stateless services and transient UI orchestration.
   - Why: the interactive results pager must resolve the new path without creating duplicate API clients, writers, or persistence policies.
   - Verify: resolve `ISearchFlow`, `ISearchResultsPager`, the categorization flow/service, the shared Carrot client, both existing export paths, and the categorized export graph from the production service collection. Confirm existing Process Documents services still resolve.

7. **Update operator documentation and embedded help.**
   - Files/symbols: `src/Carrot.Cli/Docs/isearch.md`; `src/Carrot.Cli/Docs/process.md` only for shared Carrot behavior; `src/Carrot.Cli/Docs/privacy.md`; `src/Carrot.Cli/Docs/output-columns.md`; `docs/cli-reference.md`; `docs/output-format.md`; `README.md`; relevant help/menu assertions.
   - Change: document that categorization is explicit, uses only records loaded in the current iSearch session, submits the four named fields to the selected Carrot endpoint, can operate on a partial walk, preserves Carrot memberships, and offers a local categorized Excel workbook with no sidecar/log output. Explain that **Fetch All Pages** remains a separate explicit action.
   - Why: the feature sends research identifiers and text to a configurable service and creates a new local artifact, so data flow and privacy boundaries must be visible in deployed help.
   - Verify: embedded-resource/help tests pass; stale claims that iSearch results cannot be categorized or saved are removed; existing raw-export and document/file documentation remains accurate.

8. **Run focused and full verification.**
   - Run focused shared-Carrot, iSearch mapping/categorization, pager, categorized pager, report mapper, exporter, Excel writer, composition, and documentation tests first.
   - Run `dotnet build .\Carrot-CLI.slnx --no-restore --disable-build-servers -m:1 --verbosity minimal`, `dotnet test .\Carrot-CLI.slnx --no-build --no-restore --verbosity normal`, `dotnet format .\Carrot-CLI.slnx --verify-no-changes --no-restore`, and `git diff --check`.
   - Confirm XML documentation and documentation-convention tests cover every materially modified C# member, including any extracted shared categorization contracts.
   - Perform an optional bounded manual smoke test only when a local Carrot 4.8.6 service and iSearch User Secrets are available: load a small query, fetch all pages if desired, categorize, inspect category display, save the categorized workbook, and verify that the workbook contains only the four requested iSearch fields plus provenance/category columns. Do not log credentials or unnecessary research content.
   - After implementation, append one verified completion entry covering every changed code, test, configuration, and code-related documentation file to `C:\Source\Programs\Journal.md`; rename this plan to `(done)` only after implementation and verification pass.

## 8. Acceptance criteria

- The interactive iSearch results menu contains an exact **Categorize iSearch Results** action after a successful response with at least one retained record.
- Selecting the action submits exactly the records in `SearchResultPageSession.WalkedResults`, in service-page/result order, without fetching another iSearch page.
- Every Carrot document contains only `nihApplId`, `title`, `abstract`, and `specificAims`; local paths, query metadata, unrelated return fields, and fabricated content are absent.
- Missing or invalid required iSearch fields fail before Carrot is contacted and produce safe structured diagnostics. Valid null values are retained according to the tested mapping contract.
- Carrot `/list` validation, clustering selection defaults, endpoint validation, timeout, retry, redirect, cancellation, and response-contract behavior use the same shared pathway as document/file categorization.
- Carrot response indexes correlate to the exact submitted iSearch record positions; every record receives a categorized row, including unassigned records, and overlapping/nested memberships remain intact.
- A successful categorization opens a categorized-results view with literal iSearch values, assigned/unassigned state, membership counts, category paths, scores, Back/Escape behavior, and **Save Categorized iSearch Results to Excel**.
- Categorized Excel output uses one `Results` worksheet with the agreed deterministic columns, preserves record/page order, expands memberships without dropping records, stores strings safely as text, and uses shared overwrite/atomic-write behavior.
- Saving categorized results never contacts iSearch or Carrot and does not alter the active iSearch session, raw export state, or previously loaded records.
- Existing document/file preparation, categorization, processed-results display, and processed Excel export behavior remain unchanged except for tested internal reuse.
- Focused tests, full build/test/format/whitespace checks, and any available bounded smoke test pass; unavailable live-service checks are explicitly reported.

## 9. Risks and assumptions

- The plan assumes the Carrot 4.8.6 document contract accepts arbitrary peer fields through the existing `ClusterDocument.AdditionalFields` extension-data mechanism. The implementation must confirm this against the repository's authoritative Carrot contract/tests before locking the serializer mapping.
- The plan assumes the four requested iSearch properties are present in the configured `Grants.DefaultFields` list, which is true in the checked-in `src/Carrot.Cli/appsettings.json`. If another return dataset is selected without those fields, categorization fails safely rather than submitting a different schema.
- The current result session can retain up to the existing Excel-safe row bound. Carrot may still reject very large one-request payloads or exceed practical clustering time; the feature should report the shared Carrot failure and must not silently split the request.
- `nihApplId` may be represented as a string or number by iSearch. The adapter preserves its JSON value in the categorized row, sends a Carrot-compatible invariant string, and the workbook mapper chooses a tested safe Excel representation rather than culture-dependent text conversion.
- Carrot scores are relative to one response and are not comparable across separate categorization runs. The categorized view and workbook should retain the existing score wording and invariant unrounded representation.
- Categorization of a partial walk is intentional but potentially incomplete. The UI and workbook must expose loaded result count/provenance clearly enough that operators do not mistake it for a full query export.
- No live iSearch or Carrot call is required to create this plan. Any implementation smoke test must use configured credentials/endpoints, honor the iSearch authenticated request interval, and avoid exposing credentials or sensitive result content.

## 10. Deferred follow-up

- Add a user-selected clustering algorithm/language/template prompt to the iSearch categorization action; this phase uses the established interactive defaults unless the existing shared selection flow is already exposed without duplication.
- Add automatic “fetch all, then categorize” orchestration; the current feature keeps fetching and categorizing as separate explicit actions.
- Add resumable categorized sessions, cached Carrot responses, cross-query category comparison, category deduplication, or incremental re-categorization.
- Add a named/noninteractive iSearch categorize command or scheduler integration.
- Add additional iSearch fields, configurable category workbook columns, charts, dashboards, or multiple worksheets.

## 11. Skill usage

- `plan-software-changes`: used to ground the plan in the completed iSearch and document/file categorization pathways, define one coherent implementation phase, identify non-goals, and specify actionable verification.
