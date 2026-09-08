# Interactive iSearch Connection and Query

**Status:** Complete

## 1. Outcome

Add an **iSearch** option to the interactive main menu. Selecting it validates the locally configured iSearch credentials, checks `GET /health`, reports the sanitized availability response, discovers the service's live datasets, and—only when the service reports available—opens a menu that lets the operator select a dataset and submit a bounded query against it.

## 2. Problem

`InteractiveMenu` currently offers Process Documents, Preview Request, Server Information, Help, About, and Exit only. Its existing API boundary (`ICarrotApiClient`) and `ServerInformationFlow` are specific to the Carrot `/list` and `/cluster` contracts, so they cannot safely represent iSearch health, dataset discovery, or search results. Although the executable project already has a `UserSecretsId` and uses the Generic Host configuration pipeline, it has no iSearch options, authenticated HTTP client, API contracts, interactive flow, or tests.

## 3. Solution Vision

Create an isolated `ISearch` integration namespace and a thin `ISearchFlow` UI coordinator. The integration client owns the NIH iSearch base URI (`https://isearch.opa-tools.od.nih.gov/api/`), the `apiKey` cookie, JSON parsing, timeouts, redirect refusal, rate-limit behavior, and sanitized failures. The UI owns only prompts, menu navigation, and result presentation.

When an operator chooses **iSearch** from the main menu, `ISearchFlow` will first verify that a nonempty `iSearch:apiKey` is available from the layered application configuration before any GET request. It will then call `/health` and display the returned availability payload. On a successful available response, it will call `/datasets`, present the exact returned database names, retain the selected database for that visit, and offer **Select Database**, **Submit Query**, and **Back to Main Menu** actions. Submitting a query will prompt for a nonempty query string and call `POST /search` with the selected database, the query, `defaultOp: "AND"`, and a bounded `rows` value of 100 or fewer. It will display the returned count metadata and a readable JSON representation of the returned records without persisting results.

This keeps the new public-service dependency independent from the existing Carrot2 client, preserves the existing interactive-menu navigation model, and uses live iSearch discovery rather than hard-coding database or field names.

## 4. Scope

- Add a main-menu iSearch route and its interactive availability, dataset-selection, query-submission, failure, Back, and cancellation behavior.
- Bind iSearch settings from User Secrets through the existing host configuration, without committing a secret file or credential.
- Add a dedicated authenticated iSearch HTTP client, health/dataset/search contracts, safe response rendering, and service registration.
- Use `/health` to check availability, `/datasets` for selectable database names, and `POST /search` for a bounded free-text query.
- Add focused unit and interactive tests plus user-facing help/reference updates for the new workflow and prerequisite configuration.

## 5. Non-goals

- Add named `isearch` command-line routes, saved searches, fetch-by-ID, record export, pagination, cached databases, or remembered database selections.
- Hard-code a dataset, database field, query field, or field-qualified query syntax.
- Send document content to iSearch, modify existing Carrot2 workflows, or persist credentials, queries, datasets, or results.
- Create or copy `secrets.json` into the repository.

## 6. Technical Approach

- **Configuration:** Introduce an internal `ISearchOptions` binding for `iSearch`. Read the API key only through `IOptions`/`IConfiguration`, never from a file path. Do not validate it at host startup: the rest of Carrot CLI must continue working for operators who do not use iSearch. Instead, the iSearch client must reject a missing or whitespace-only key locally before constructing or sending every GET request. Add `iSearch:contactEmail` as a required configuration prerequisite for the iSearch flow and send it as the `From` header; the NIH API guidance requires a monitored contact address for repeated requests. Document both User Secrets values without exposing values.
- **HTTP boundary:** Add `IISearchApiClient` and `ISearchApiClient` beside a dedicated contracts folder. Register it with `AddHttpClient`, the fixed API base address, a long-lived `HttpClient`, JSON `Accept` negotiation, and redirects disabled. Send the API key only as the `apiKey` cookie on the iSearch host; do not add it to URLs, logs, exceptions, test output, or diagnostics. Use explicit request timeouts, propagate cancellation, return structured `OperationResult` failures, limit sanitized response bodies, honor `Retry-After`/`429`, and pace sequential authenticated requests at least one second apart. Authentication, authorization, and validation errors must not be retried unchanged.
- **Availability and discovery:** Model the `/health` response tolerantly enough to report its returned status/payload, but treat only an HTTP-successful, positive service state (expected `status: "UP"`) as permission to continue. Then call `/datasets` and use its returned JSON string array as the sole source of selectable databases. If the list is empty, display that outcome and return to the iSearch menu without enabling a query.
- **Queries:** Use `POST /search` rather than a query-string GET so free-text input is safely serialized in a JSON body. The first phase accepts an unqualified free-text/Lucene query only; it does not promise field-qualified syntax because fields have not yet been discovered with `GET /fields/{dataset}`. Request no more than 100 records, set `defaultOp` to `AND`, and render `returnedCount`, `totalCount`, and the returned `results` in a terminal-height-aware view. A later phase can add field discovery and paging from the response cursor.
- **Alternatives considered:** Reusing `ICarrotApiClient` would couple unrelated base URIs, authentication schemes, and JSON contracts, so a separate client is preferred. A raw `HttpClient` call in `InteractiveMenu` would make secrets, request safety, and error handling hard to test, so the flow delegates network work. Sending a GET search was rejected because POST keeps query encoding out of manually composed URLs and accommodates future query controls.

## 7. Implementation Steps

1. **Add iSearch configuration and registration.**
   - Add `src/Carrot.Cli/Configuration/ISearchOptions.cs` and a narrowly scoped validator/helper that can report missing `apiKey` or `contactEmail` only when the iSearch feature is invoked.
   - Update `src/Carrot.Cli/Composition/ServiceRegistration.cs` to bind the `iSearch` section, register the iSearch typed `HttpClient`, the API client, result renderer/pager, and `ISearchFlow`.
   - Keep `src/Carrot.Cli/appsettings.json` free of secrets; add only non-sensitive defaults if the implementation needs an explicit, documented timeout. Preserve the existing configuration behavior for all non-iSearch commands.
   - Verify with configuration tests that valid User-Secrets-style values bind, a missing key prevents outbound GETs, and loading the host with no iSearch configuration does not break existing CLI commands.

2. **Implement the isolated iSearch API boundary and contracts.**
   - Add `src/Carrot.Cli/ISearch/IISearchApiClient.cs` and `src/Carrot.Cli/ISearch/ISearchApiClient.cs`, with explicit operations for health, datasets, and search.
   - Add response/request types under `src/Carrot.Cli/ISearch/Contracts/` for health, the dataset string collection, search request, and the documented `cursor`, `returnedCount`, `totalCount`, and `results` response envelope. Preserve unrecognized record fields as JSON rather than inventing per-database models.
   - Ensure each GET validates the key before a request is created; attach the cookie and `From` header without logging either. Validate HTTP success and response shape before passing values to the UI; translate failures into safe `OperationResult` messages that identify the operation and status without exposing sensitive headers or an unbounded error body.
   - Cover request cookie/header behavior, base URI/path construction, redirect refusal, missing credentials, malformed/success/error responses, cancellation, bounded retry/rate-limit behavior, and POST body serialization in new `tests/Carrot.Cli.Tests/ISearch/` tests using a fake `HttpMessageHandler`.

3. **Create the interactive flow and result presentation.**
   - Add `src/Carrot.Cli/Cli/UI/ISearchFlow.cs` plus a focused `ISearchResultsPager` or renderer that uses the existing Spectre console patterns and literal text/escaped JSON output.
   - On `RunAsync`, report that iSearch availability is being checked, call health, show its returned availability response, and stop safely on missing configuration, HTTP failure, malformed data, or non-positive health status. Do not make dataset or search calls after an unsuccessful availability check.
   - After a positive health check, load `/datasets`; show an iSearch submenu that has a clear current-database indicator, **Select Database**, **Submit Query**, and **Back to Main Menu**. Database selection must use the exact discovered string values and query submission must remain unavailable until one is selected.
   - Prompt for an unqualified nonempty query, submit a bounded POST search for the selected database, and page/summarize the response without files or mutable cross-workflow state. Return from results to the iSearch submenu; Escape/Back must return through the existing menu hierarchy, and cancellation must propagate to `InteractiveMenu` for exit code `130`.
   - Verify with `TestConsole` tests for main-menu routing; positive and negative health; the displayed availability payload; no calls past a negative health result; live dataset choices; selected-database retention; missing-database query prevention; query request contents; result count/record display; failures; Escape/Back; and cancellation.

4. **Wire navigation and update documentation.**
   - Update `src/Carrot.Cli/Cli/UI/InteractiveMenu.cs` to add an `ISearch` main-menu choice, constructor dependency, dispatch branch, and display label while retaining every existing menu route.
   - Add `src/Carrot.Cli/Docs/isearch.md`, register it in `HelpTopicCatalog`, and update `src/Carrot.Cli/Docs/getting-started.md`, `src/Carrot.Cli/Docs/troubleshooting.md`, `README.md`, and `docs/cli-reference.md`. Explain how to set the two User Secrets values, that the key is sent only as a cookie, that entry checks `/health`, that datasets are discovered live, the 100-record query limit, and that no results are written.
   - Verify the new topic renders both from the Help menu and `carrot-cli help isearch`; ensure no documentation, examples, fixtures, logs, or test assertions contain a real key.

5. **Perform repository verification and record completion.**
   - Run the focused iSearch and interactive-menu tests first, then `dotnet build .\Carrot-CLI.slnx --no-restore`, `dotnet test .\Carrot-CLI.slnx --no-build --no-restore`, `dotnet format .\Carrot-CLI.slnx --verify-no-changes --no-restore`, and `git diff --check`.
   - Manually exercise the interactive path with a configured development key: observe the health response, select a returned database, submit a harmless bounded query, and confirm no key/cookie is displayed. Do not include the key in verification output.
   - Before implementation is declared complete, append one verified task entry to `C:\Source\Programs\Journal.md` that lists every changed code, test, configuration, and code-related documentation file; do not journal this planning-only document.

## 8. Acceptance Criteria

- The interactive main menu includes **iSearch**, and selecting it does not affect existing Carrot2 menu routes.
- The flow refuses to send any GET when `iSearch:apiKey` is absent or blank, reports a clear configuration error, and never displays secret values.
- With valid iSearch configuration, entering the flow calls `/health`, shows the returned sanitized availability response, and calls `/datasets` only after a positive health result.
- The dataset picker contains only live `/datasets` values; a database must be selected before **Submit Query** can run.
- A submitted nonempty query is sent as bounded JSON `POST /search` for the selected database with `defaultOp: AND`, and its returned counts and records are readable without writing files.
- Failed health/discovery/query calls, malformed JSON, redirects, rate limits, missing configuration, Back/Escape, and cancellation are handled safely; no request leaks credentials in output or logging.
- New and changed code follows the repository's C# documentation and implementation-region conventions; focused tests, full build/test, format verification, whitespace check, and the manual configured-key smoke test pass.

## 9. Risks and Assumptions

- **Assumption:** “check the location for availability” means the iSearch service availability endpoint (`GET /health`), not a geographic location lookup. The plan treats a successful positive health payload as the required gate.
- **Configuration prerequisite:** The requested `iSearch:apiKey` exists in User Secrets; the live service guidance also requires a monitored `iSearch:contactEmail` for the `From` header. Implementation needs that non-secret value to be configured before it can make iSearch calls.
- iSearch database names and record schemas are service-owned and may change. The feature therefore discovers datasets per entry and preserves result records as generic JSON.
- Query text can be expensive or invalid. This phase bounds rows to the authenticated maximum, does not export or page indefinitely, and returns server validation feedback safely. Field-qualified query assistance waits for a future `/fields/{dataset}` discovery feature.
- A health-success-to-search failure remains possible because the service can change state between calls; each subsequent response is independently validated.

## 10. Deferred Follow-up

- Named noninteractive iSearch commands with automation-friendly exit codes and output formats.
- Dataset field discovery, type-aware query construction, result-field selection, sorting, paging, saved-search execution, and fetch-by-ID.
- Optional result export or a privacy-reviewed integration that turns selected iSearch records into Carrot document inputs.
