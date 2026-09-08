# Interactive iSearch Field Discovery

**Status:** Complete

## 1. Outcome

Add a **View Fields** action to the interactive iSearch dataset menu. After the operator selects a live dataset, the CLI calls the authenticated `GET /fields/{dataset}` endpoint and displays the returned field metadata in a terminal-safe table with the same columns and ordering as the updated PowerShell field-discovery script:

`name`, `displayName`, `fieldType`, `defaultQueryField`, `defaultResultField`, `multiValued`, and `searchOnly`.

The operator can page through a large field list, return to the iSearch dataset menu, and retain the selected dataset for a subsequent query or field view.

## 2. Problem

The existing interactive iSearch workflow discovers dataset names and can submit a query, but it cannot show the schema of the selected dataset. `InteractiveISearchFlow.runDatasetMenuAsync` currently offers only **Select Database**, **Submit Query** after a selection, and **Back to Main Menu**. `IISearchApiClient` and `ISearchApiClient` expose health, dataset discovery, and search operations, but no `/fields/{dataset}` operation or field contract.

The external `C:\Source\PS\iSearch-API-Test.ps1` script demonstrates the required operator-facing view: it requests `/fields/grants`, sorts by `name`, and displays the seven metadata properties listed above. The CLI needs the same capability inside the selected-dataset workflow so an operator does not have to leave the application or maintain a separate script.

## 3. Solution vision

Extend the existing isolated iSearch boundary with a typed field-discovery operation and a focused table pager. The API client remains responsible for authenticated HTTP, path encoding, cancellation, response validation, and safe operation failures. The interactive flow remains responsible only for menu state and invoking presentation services. A dedicated fields pager owns table layout and paging, keeping `SearchResultsPager` focused on generic JSON search records.

The flow will be:

1. Enter iSearch, validate configuration, check health, and discover datasets using the existing behavior.
2. Select a dataset from the live dataset list.
3. The dataset menu enables **View Fields** and **Submit Query**.
4. **View Fields** calls `GET /fields/{dataset}` using the retained dataset name.
5. The fields pager sorts records by `name`, renders the required seven columns, pages within the terminal display budget, and returns to the dataset menu on Back or Escape.

This preserves the current dependency direction: the UI depends on `IISearchApiClient` and presentation abstractions, while the HTTP implementation does not depend on Spectre.Console or interactive state. It also keeps field metadata separate from generic search result JSON, even though both originate from the service-owned dataset schema.

## 4. Scope

- Add a field metadata contract for the properties required by the PowerShell display and any optional service metadata needed for tolerant deserialization.
- Add `GetFieldsAsync` to the iSearch client abstraction and implement authenticated `GET /fields/{dataset}` retrieval with encoded dataset paths and bounded response handling.
- Add **View Fields** to the selected-dataset submenu; keep it unavailable until a dataset is selected.
- Add a terminal-safe, paged table renderer with the exact seven display columns and `name` ordering used by the PowerShell script.
- Add focused API-client and interactive-flow tests for retrieval, validation, rendering, navigation, cancellation, and safe failures.
- Update iSearch user documentation and the CLI reference to describe field discovery and the displayed metadata.

## 5. Non-goals

- Add field selection to the query prompt, field-qualified query construction, query-field autocomplete, filters, sorting, or result-field selection.
- Change the existing search route, query semantics, result rendering, dataset discovery, authentication model, or main-menu route.
- Cache field metadata across visits or persist fields, datasets, queries, or search results.
- Export field metadata to JSON, CSV, or other files.
- Change the external PowerShell script; use its output shape as the CLI display contract.
- Add field descriptions, search variants, or arbitrary service properties as visible columns unless a later requirement expands the display contract.

## 6. Technical approach

### API contract and HTTP boundary

- Add an internal field contract under `src/Carrot.Cli/ISearch/Contracts/`, preferably `SearchField.cs`, with properties for `name`, `displayName`, `fieldType`, `defaultQueryField`, `defaultResultField`, `multiValued`, and `searchOnly`.
- Preserve service-owned `fieldType` strings exactly as returned. The live `grants` response has used values such as `id`, `string`, `html`, `boolean`, `int`, `float`, `date`, and `score`; do not normalize them to the narrative guide’s uppercase examples or infer unsupported types.
- Add `GetFieldsAsync(string dataset, CancellationToken)` to `IISearchApiClient`. Validate a nonempty dataset before creating a request, URL-encode the dataset path segment, and use the existing authenticated GET pipeline, timeout, cancellation, redirect refusal, pacing, retry, bounded body, and structured failure behavior.
- Parse the response as a JSON array. Require each item to have a nonempty `name`; retain optional metadata as nullable/default values if the service omits a display label or one of the Boolean flags. Treat a non-array response or invalid required field name as a safe malformed-response failure.
- Keep the request as `GET /fields/{dataset}`. Do not put the API key in the URI or diagnostic text; continue sending it only as the `apiKey` cookie and the monitored contact address as `From`.

### Menu and presentation

- Extend the private `DatasetChoice` enum in `InteractiveISearchFlow` with a field-view action. The visible label should be **View Fields**.
- Build the menu choices in this order: **Select Database**, **View Fields** when a dataset is selected, **Submit Query** when a dataset is selected, and **Back to Main Menu**. This keeps both dataset-dependent actions unavailable before selection and makes the current selected dataset clear in the title.
- Add `ISearchFieldsPager` and `SearchFieldsPager` beside the existing results pager. The flow should await the pager after a successful field request and then return to the dataset menu with the selected dataset unchanged.
- Render a table whose columns correspond exactly to the PowerShell script: `name`, `displayName`, `fieldType`, `defaultQueryField`, `defaultResultField`, `multiValued`, and `searchOnly`. Sort rows ordinally by `name` before paging. Preserve Boolean values as `True`/`False` table values and preserve blank optional text values without fabricating descriptions.
- Reserve terminal rows for the pager prompt, calculate a bounded page size from the existing console height, and provide **Next Page**, **Previous Page**, and **Back to iSearch** actions. A zero-field response should render a clear “no fields” message and still provide a Back action.
- Use Spectre.Console table/prompt APIs consistently with `SearchResultsPager`; ensure field names and metadata are rendered as literal values so service-provided text cannot be interpreted as terminal markup.

### Alternatives considered

- Reusing `SearchResultsPager` would avoid a new presenter but would display JSON records rather than the required seven-column table and would not match the PowerShell operator experience. A dedicated fields pager is preferred.
- Returning raw `JsonElement` objects would tolerate schema changes but would push service-shape parsing and column selection into the UI. A small typed contract gives the API boundary a clear validation point while preserving the service-owned type string.
- Calling `/fields/{dataset}` directly from `InteractiveISearchFlow` would duplicate credential, path, timeout, and failure logic. The API-client abstraction keeps network behavior isolated and testable.

## 7. Implementation steps

1. **Add the field contract.**
   - Create `src/Carrot.Cli/ISearch/Contracts/SearchField.cs`.
   - Document the contract and each displayed property according to the repository’s C# documentation conventions.
   - Represent `fieldType` as the service-returned string and the four display flags as Booleans, with nullable handling only where the live response contract permits omission.
   - Verify deserialization preserves representative metadata such as `grantNumber`, `displayName`, `string`, and each Boolean flag.

2. **Add authenticated field discovery to the API client.**
   - Update `src/Carrot.Cli/ISearch/IISearchApiClient.cs` with `GetFieldsAsync` and its response contract documentation.
   - Update `src/Carrot.Cli/ISearch/ISearchApiClient.cs` with the `/fields/{dataset}` request, dataset path encoding, array parsing, required-name validation, and safe malformed/error results.
   - Reuse the existing GET credential gate and request pacing. Do not add a second HTTP client or a separate secret-loading path.
   - Extend `tests/Carrot.Cli.Tests/ISearch/ISearchApiClientTests.cs` to cover the encoded path, GET method, authentication headers, parsed field metadata, missing credentials, malformed arrays, empty names, redirects, cancellation, and bounded non-success diagnostics. Keep test credentials synthetic.

3. **Add the fields pager.**
   - Create `src/Carrot.Cli/Cli/UI/ISearchFieldsPager.cs` and `src/Carrot.Cli/Cli/UI/SearchFieldsPager.cs`.
   - Sort fields by `name`, render the exact seven columns, reserve prompt space, and page long lists using the established Back/Next/Previous pattern.
   - Handle an empty list, a single-page list, a multi-page list, Escape, and cancellation without mutating the selected dataset.
   - Register the implementation in `src/Carrot.Cli/Composition/ServiceRegistration.cs` and inject it into `InteractiveISearchFlow`.

4. **Add the selected-dataset menu action.**
   - Update `src/Carrot.Cli/Cli/UI/InteractiveISearchFlow.cs` to add **View Fields**, invoke `GetFieldsAsync` for the retained dataset, write safe operation messages on failure, and open the fields pager on success.
   - Keep **View Fields** and **Submit Query** absent before selection. After returning from field display, preserve the selected dataset and the existing query action.
   - Extend `tests/Carrot.Cli.Tests/ISearch/InteractiveISearchFlowTests.cs` with deterministic field metadata and keyboard input covering field-view selection, no call before dataset selection, exact column labels/values, sorted rows, return to the dataset menu, retained selection, empty fields, field failures, Escape, and cancellation.

5. **Update user-facing documentation.**
   - Update `src/Carrot.Cli/Docs/isearch.md` to document **View Fields**, the authenticated `GET /fields/{dataset}` route, the seven displayed columns, live field-name/type ownership, and paging behavior.
   - Update `docs/cli-reference.md` if needed to describe the expanded iSearch submenu while preserving the existing help catalog and embedded-document workflow.
   - Do not document a fixed list of fields as a permanent schema; use examples such as `name`, `displayName`, and `fieldType` only to explain the metadata shape.

6. **Verify the complete feature.**
   - Run focused field and iSearch tests first.
   - Run `dotnet build .\Carrot-CLI.slnx --no-restore`, `dotnet test .\Carrot-CLI.slnx --no-build --no-restore`, `dotnet format .\Carrot-CLI.slnx --verify-no-changes --no-restore`, and `git diff --check`.
   - With configured credentials, manually select iSearch, select `grants`, choose **View Fields**, confirm the table contains the seven columns and live rows such as `grantNumber`, page through the list, return to the dataset menu, and confirm **Submit Query** remains available for the retained dataset. Do not print credentials or cookies.
   - Record every implementation, test, configuration, and documentation file changed during implementation in the required global journal after verification. This pending plan is planning-only and should not be included in that future implementation entry.

## 8. Acceptance criteria

- After a live dataset is selected, the iSearch submenu contains **View Fields**; before selection it does not.
- Choosing **View Fields** calls authenticated `GET /fields/{dataset}` using the exact selected dataset with a correctly encoded path segment.
- The response is validated as a field array and rendered sorted by `name` with exactly these visible columns: `name`, `displayName`, `fieldType`, `defaultQueryField`, `defaultResultField`, `multiValued`, and `searchOnly`.
- The displayed `fieldType` value is the service-returned string, including lowercase or other live values, and Boolean metadata is displayed accurately without inferred defaults that contradict the response.
- Long field lists page within the terminal, and **Back to iSearch**/Escape returns to the dataset menu while retaining the selected dataset.
- Empty field lists, malformed responses, HTTP failures, missing credentials, redirects, cancellation, and transport/rate-limit outcomes are handled through the existing safe iSearch result/error conventions without leaking credentials.
- Existing health, dataset selection, search submission, result paging, main-menu navigation, and cancellation behavior remain unchanged.
- Focused tests, the full build/test suite, format verification, whitespace verification, and a configured-key manual field-display smoke test pass.

## 9. Risks and assumptions

- iSearch owns dataset schemas and may add, remove, rename, or reorder fields. The client must discover fields per visit and must not hard-code a complete field catalog.
- Field metadata can be large, especially for `grants`; paging is required to keep the terminal usable and avoid an unbounded single render.
- The live service may return field types not listed in the narrative guide. Displaying the raw string avoids lossy normalization but postpones type-aware query assistance to a separate feature.
- The field endpoint is another authenticated GET and is subject to the service’s pacing, retry, cancellation, and rate-limit rules.
- The plan assumes “View Fields” should be enabled only after dataset selection and should return to the existing dataset menu rather than the main menu.

## 10. Deferred follow-up

- Type-aware query prompts and validation based on `fieldType`.
- Query-field, filter-field, sort-field, and result-field selection using the discovered metadata.
- Displaying full field descriptions, search variants, grouping, or search-assist metadata.
- Caching field metadata during or across CLI visits.
- Exporting field metadata or exposing it through a noninteractive command.
