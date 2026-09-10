# Named iSearch Command-Line Operations

**Status:** Pending

## 1. Outcome

Add a noninteractive `isearch` command that automates the existing interactive iSearch workflow
without replacing it. The command will accept the live iSearch database, the configured result
dataset, query text, either a bounded maximum-result count or an explicit all-results mode, optional
original-result Excel output, an optional Carrot categorization step, and optional categorized-result
Excel output. In all-results mode, the command will cursor-walk the database until iSearch's
reported total is loaded, subject only to the existing hard storage and workbook safety limits.

Representative invocations will be:

```powershell
# Search one live iSearch database and save the returned pages to Excel.
carrot-cli isearch `
  --database grants `
  --result-dataset Grants `
  --query "title:pain" `
  --max-results 300 `
  --output "C:\Results\grants-original.xlsx" `
  --overwrite

# Search, send the loaded records to Carrot, and save both workbooks.
carrot-cli isearch `
  --database grants `
  --result-dataset Grants `
  --query '"lung cancer"' `
  --max-results 5000 `
  --output "C:\Results\grants-original.xlsx" `
  --categorize `
  --categorized-output "C:\Results\grants-categorized.xlsx" `
  --endpoint "http://localhost:8080/service" `
  --overwrite

# Cursor-walk every page reported by iSearch without guessing a result count.
carrot-cli isearch `
  --database grants `
  --result-dataset Grants `
  --query "title:pain" `
  --all-results `
  --output "C:\Results\all-grants.xlsx" `
  --overwrite
```

The command will remain prompt-free and return stable nonzero exit codes for invalid options,
iSearch failures, Carrot categorization failures, incomplete bounded walks, output failures, and
cancellation. Authentication will continue to come from the existing `iSearch:apiKey` and
`iSearch:contactEmail` User Secrets configuration; no API-key option will be added.

## 2. Problem

The current command surface is named `process`, `preview`, `server-info`, `help`, and `about`,
while iSearch is available only from the interactive menu. The interactive path already supports
health validation, live database discovery, configured return-dataset selection, cursor-based page
fetching, Carrot categorization, and explicit Excel export, but those actions require prompts and
cannot be run by Task Scheduler or another automation wrapper.

The existing implementation provides the reusable boundaries needed for a command:

- `src/Carrot.Cli/ISearch/ISearchApiClient.cs` owns authenticated health, dataset, search, and
  cursor-continuation calls.
- `src/Carrot.Cli/ISearch/SearchResultPageSession.cs` retains accepted pages and records while
  preserving the original query context and page invariants.
- `src/Carrot.Cli/Configuration/SearchReturnTypeCatalog.cs` loads the configured
  `iSearchReturnTypes.Results` children and their ordered `DefaultFields`.
- `src/Carrot.Cli/ISearch/SearchResultsCategorizer.cs` maps loaded records to the exact four-field
  Carrot request and delegates to the shared Carrot categorization service.
- `src/Carrot.Cli/Reporting/SearchResultsExporter.cs` and
  `src/Carrot.Cli/Reporting/CategorizedISearchResultsExporter.cs` already use the shared atomic
  Excel workbook writer.

What is missing is a noninteractive settings contract and orchestration boundary that composes
those services without copying interactive prompts, creating a second HTTP client, or introducing
a second Excel format.

## 3. Solution vision

Register one typed Spectre.Console.Cli command named `isearch`. Its settings will distinguish the
two iSearch concepts that otherwise have ambiguous names:

- `--database <NAME>` identifies the live iSearch service database and must match a value returned
  by authenticated `GET /datasets`.
- `--result-dataset <NAME>` identifies a configured child under
  `iSearchReturnTypes.Results` such as `Grants`; its ordered `DefaultFields` become the iSearch
  `fl` parameter.

The command will execute this flow without prompts:

1. Validate Spectre-bound option relationships, iSearch credentials, return-dataset configuration,
   output paths, the mutually exclusive maximum-result/all-results selection, and
   categorization/output dependencies locally.
2. Call `GET /health`, then `GET /datasets`; stop before search if health is not positive or the
   requested database was not discovered.
3. Resolve the configured result dataset and submit the first bounded search with the requested
   query, ordered fields, `defaultOp=AND`, and no more than 100 rows per service request.
4. Use `SearchResultPageSession` to cursor-walk service pages until the accepted record count reaches
   `--max-results`, or until the first response's stable `totalCount` is reached in
   `--all-results` mode. Continue with the current cursor and unchanged request context only.
5. If `--output` was supplied, save all accepted pages through `SearchResultsExporter` and the
   existing `IExcelWorkbookWriter` contract.
6. If `--categorize` was supplied, send only the accepted records through
   `ISearchResultsCategorizer` and the shared Carrot categorization path. If
   `--categorized-output` was supplied, save the successful categorized batch through
   `CategorizedISearchResultsExporter`.
7. Emit a concise machine-readable-friendly summary containing the database, configured result
   dataset, query outcome, loaded/total counts, whether the run was capped or complete, and saved
   absolute paths. Do not print record payloads or credentials by default.

The command class will remain a thin Spectre adapter. A dedicated iSearch command workflow will
own orchestration and return a structured result so the command is testable without HTTP or file
side effects. Reporting will remain separate from HTTP, categorization, and workbook persistence.
This preserves the existing dependency direction: command settings and CLI reporting depend on the
workflow contract; the workflow depends on iSearch, Carrot, configuration, and reporting
abstractions; the existing interactive UI continues to depend on the same feature services.

## 4. Scope

- Add the named `isearch` command and generated Spectre help metadata.
- Add explicit settings for database, configured result dataset, query, bounded maximum-result or
  explicit all-results cursor walking,
  original Excel output, categorization, categorized Excel output, overwrite behavior, and the
  existing Carrot endpoint/timeout needed by the categorization path.
- Reuse the existing iSearch authentication from User Secrets, live health/database discovery,
  configured return-field selection, cursor-aware page session, Carrot categorizer, and atomic
  Excel exporters.
- Add a noninteractive workflow that never prompts, never exposes credentials, and only performs a
  complete cursor walk when the caller explicitly requests `--all-results`.
- Define automation-oriented exit-code behavior and safe console diagnostics.
- Update embedded iSearch/help topics, command reference material, README content, output guidance,
  and Task Scheduler/troubleshooting examples for named iSearch automation.
- Add focused Spectre command, workflow, output, and documentation/resource tests, then run the
  normal repository verification.

## 5. Non-goals

- Do not remove or redesign the interactive iSearch menu, result pager, field viewer, categorization
  flow, or interactive export prompts.
- Do not add a second iSearch HTTP client, another authentication mechanism, an API-key CLI option,
  or a repository-local `secrets.json`.
- Do not accept arbitrary result fields in this command; `--result-dataset` resolves fields from
  the validated `iSearchReturnTypes.Results` configuration.
- Do not add automatic field discovery, query construction, saved-search execution, fetch-by-ID,
  or a new iSearch output format.
- Do not add a prompt-based confirmation for overwriting files. Named commands must be explicit:
  existing workbooks require `--overwrite`.
- Do not add named-command-specific Carrot algorithm, language, template, or parameter-file
  options in this phase unless implementation proves the existing categorization contract cannot
  use its configured defaults. Such selection expansion is deferred rather than duplicated.
- Do not claim a complete dataset walk when `--max-results` stops before iSearch reports completion.
  `--all-results` is the explicit opt-in for a complete walk; it is not an implicit default.

## 6. Technical approach

### Command contract

Add one leaf command rather than a nested `isearch search`/`isearch categorize` tree. The operation
is one coherent pipeline whose optional categorization and exports depend on the same retained
search session. Separate leaf commands would require a new persisted interchange contract and would
duplicate selection, paging, and validation rules.

The planned settings are:

| Option | Contract | Behavior |
| --- | --- | --- |
| `--database <NAME>` | Required nonempty string | Must exactly match a live value from `GET /datasets`. |
| `--result-dataset <NAME>` | Required nonempty string | Must exactly match a configured return-dataset child under `iSearchReturnTypes.Results`; matching is ordinal and case-sensitive unless the existing catalog contract establishes otherwise. |
| `--query <TEXT>` | Required nonempty string | Trimmed free-text or Lucene query; sent unchanged after trimming. |
| `--max-results <COUNT>` | Positive integer, default `100` when `--all-results` is absent | Bounds retained records. The command cursor-walks all needed service pages until this count is reached or iSearch is complete. |
| `--all-results` | Boolean flag, mutually exclusive with `--max-results` | Uses the first response's `totalCount` as the target and cursor-walks every required page until all results are accepted or a hard storage/workbook limit is reached. |
| `--output <PATH>` | Optional `.xlsx` file | Saves accepted original iSearch records with the existing generic `Results` worksheet format. |
| `--categorize` | Boolean flag | Runs the loaded records through the shared Carrot categorization service. Invalid when no records were loaded. |
| `--categorized-output <PATH>` | Optional `.xlsx` file | Requires `--categorize`; saves the categorized batch using the existing categorized worksheet format. |
| `--endpoint <URI>` | Optional Carrot endpoint | Uses the existing noninteractive endpoint precedence and is required when no `CARROTCLI_ENDPOINT` fallback is available and categorization is requested. |
| `--timeout-seconds <SECONDS>` | Optional positive integer | Applies the existing Carrot timeout behavior to categorization. iSearch timeout remains configuration-owned. |
| `--overwrite` | Boolean flag | Allows either requested workbook to replace an existing file. Without it, the operation fails before writes. |
| `--quiet` | Boolean flag | Suppresses the normal success summary while retaining warnings and errors, matching named process behavior. |

The implementation must verify whether `--endpoint` and `--timeout-seconds` can reuse
`EndpointSettings`/`RunSettingsResolver` without making unrelated settings mandatory. If a small
shared resolver extraction is needed, preserve the existing `CARROTCLI_ENDPOINT` precedence,
positive timeout validation, and configured Carrot clustering defaults for current commands.

`--max-results` is deliberately record-based for bounded automation. The service still returns
complete pages, so the workflow must preserve no more than the requested number of records in the
session used for Excel output and categorization. When the bound falls inside a service page, the
workflow must retain the prefix needed to honor the exact record limit while preserving the page
cursor and provenance. A default of 100 preserves a safe one-page invocation; callers can choose a
larger explicit bound for a controlled partial walk.

`--all-results` is the explicit complete-walk mode. The first validated search response supplies
`totalCount`; the workflow sets that value as the target, then follows the cursor from each accepted
page and repeats the original database, query, fields, operator, and row limit until the loaded
record count equals `totalCount`. It must stop successfully for zero results, reject an empty or
unchanged cursor before the target is reached, reject a changing total, and honor the configured
authenticated request interval. This removes the need for the operator to guess an arbitrary
maximum-result value while still making the potentially expensive behavior visible in the command
line.

The all-results mode is bounded by a system safety ceiling rather than a caller-selected result
value. The existing session/workbook capacity of 1,048,575 data rows is the minimum hard guard for
Excel-backed retention. If iSearch reports more results than the supported retention or a Carrot
categorization request can safely represent, the workflow must fail clearly before claiming or
writing a complete artifact; it must not silently truncate an all-results run. A separate future
chunked-categorization design may relax the Carrot-side limit.

### Authentication and iSearch safety

Continue the existing `Program` configuration path: `Host.CreateApplicationBuilder(args)` loads
the project User Secrets through `builder.Configuration.AddUserSecrets<Program>(optional: true)`,
and `ISearchOptions` receives the `iSearch` section. The command must invoke
`ISearchOptionsValidator` before any iSearch request. The existing API client remains responsible
for rejecting a missing key before every GET, sending the key only in the `apiKey` cookie, adding
the monitored `From` header, refusing redirects, honoring the configured one-second authenticated
pace, and sanitizing response failures.

The workflow must call health before database discovery and database discovery before search. It
must validate the requested database against the live response rather than trusting the command
argument or hard-coding a service database. It must use the configured result dataset's fields and
the service-owned response envelope (`cursor`, `returnedCount`, `totalCount`, `results`) without
inventing per-database record DTOs.

### Paging and partial results

Use the existing first-page `SearchAsync` operation to create `SearchResultPageSession`. In bounded
mode, call `FetchNextAsync` repeatedly while the retained record count is below `--max-results` and
`CanFetchNextPage` is true. In all-results mode, continue while the session has not reached the
first response's `totalCount` and a valid cursor remains. Each continuation must use the session's
current cursor and unchanged `SearchRequest`; the workflow must not issue a fresh first-page search
inside the loop. Stop successfully when the requested bound or service total is reached.

Because iSearch continuation is page-based, a response can contain more records than remain in the
requested bound. Extend the session or add a bounded retained-result projection so the workflow can
accept only the required prefix for export and categorization while retaining the service cursor and
page number needed for accurate provenance. The projection must keep response counts internally
consistent, preserve service order and duplicates, and distinguish a caller-imposed cap from a
service-complete walk. Do not issue another cursor request after the cap is reached.

If the maximum-result bound is reached while `CanFetchNextPage` remains true, preserve the accepted
prefix and return a structured partial result. The command may still save those accepted pages and
categorize those accepted records, but it must return `ExitCodes.PartialSuccess` unless a later
output or categorization failure has a more specific failure code. In all-results mode, reaching a
hard retention ceiling, an invalid continuation, an unchanged/empty cursor before `totalCount`, or a
changing total is a failure rather than a successful partial walk; the command must not write an
artifact labeled complete. In either mode, preserve accepted state and sanitized diagnostics when a
later request fails.

### Categorization and Excel persistence

Reuse `ISearchResultsCategorizer` so the named path sends the same four source fields as the
interactive path: `nihApplId`, `title`, `abstract`, and `specificAims`. It must not fetch another
iSearch page during categorization. The Carrot endpoint is normalized through the existing
`EndpointResolver`/`RunSettingsResolver` boundary, and categorization keeps the current list-first
validation and configured default clustering selection.

Reuse `SearchResultsExporter` and `CategorizedISearchResultsExporter`, not a command-specific
writer. Normalize each supplied path through `ExcelOutputPathResolver`, require an existing parent
directory and `.xlsx` extension, reject collisions between original and categorized destinations,
and pass the caller's overwrite policy to the existing atomic writer. Original output contains all
accepted pages in service order. Categorized output retains the original four source fields and
category memberships, including an unassigned row when Carrot returns no membership.

### Exit codes and diagnostics

Preserve existing meanings for `0`, `1`, `2`, `4`, `5`, `6`, and `130`. Add dedicated constants in
`Processing/ExitCodes.cs` only if the existing codes cannot distinguish the new stages; the intended
mapping is:

- `0`: search and every requested optional operation completed successfully.
- `1`: command syntax, settings relationship, credential, return-dataset, endpoint, or output-path
  validation failed before the corresponding side effect.
- `2`: bounded search completed with useful accepted data but did not load all service pages.
- `4`: Carrot endpoint/list validation failed during categorization.
- `5`: Carrot cluster request or response mapping failed during categorization.
- `6`: original or categorized workbook persistence failed.
- `7` (new, if needed): iSearch health, database discovery, search, or cursor-continuation failure.
- `8` (new, if needed): an iSearch-to-Carrot categorization failure that cannot be mapped to the
  existing list/cluster categories.
- `130`: cooperative cancellation.

Use the repository's `OperationResult` and `OperationMessage` conventions. Do not log query record
payloads, API keys, cookies, authorization headers, or unbounded server response bodies. The normal
summary should be stable plain text that is safe to redirect; `--quiet` should not suppress errors.

### Alternatives considered

- **Reuse the interactive flows directly:** rejected because those flows prompt and depend on
  interactive console navigation. Reuse their domain services and exporters instead.
- **Put all orchestration in `ISearchCommand.ExecuteAsync`:** rejected because it would create a
  command with too many dependencies and mix parsing, paging, categorization, persistence, and
  reporting. A focused workflow keeps the command thin and the behavior unit-testable.
- **Persist JSON between separate search and categorize commands:** rejected because the request is
  one automated search/categorization pipeline and the repository already has generic in-memory
  contracts plus Excel persistence.
- **Add arbitrary `--field` options:** rejected because configured result datasets already own field
  order and the current iSearch design intentionally avoids hard-coded or unvalidated schemas.
- **Implicitly fetch all pages without an explicit opt-in:** rejected because an accidental scheduled
  invocation could trigger a large network walk and workbook/payload allocation. `--all-results`
  makes the complete walk intentional, while the existing system retention ceiling remains enforced.
- **Create another workbook writer:** rejected because the existing typed Excel framework already
  provides formula-safe cell handling, overwrite policy, and atomic promotion.

## 7. Implementation steps

1. **Add the command settings and structural registration scaffold.**
   - Add `src/Carrot.Cli/Cli/Settings/ISearchSettings.cs` with declarative
     `[CommandOption]` metadata, descriptions, defaults, and validation for required values,
     positive maximum-result limits, mutually exclusive `--all-results` behavior,
     `--categorize`/`--categorized-output` relationships, and output-path distinctions.
   - Add `src/Carrot.Cli/Cli/Commands/ISearchCommand.cs` as an `AsyncCommand<ISearchSettings>`
     using the installed Spectre.Console.Cli 0.55 signatures and constructor injection.
   - Add a workflow contract and result/request models under `src/Carrot.Cli/ISearch/`, preferably
     `ISearchCommandWorkflow.cs`, `SearchCommandRequest.cs`, and `SearchCommandResult.cs`, so the
     command adapter has one focused dependency and the result can describe accepted pages,
     categorization, partial completion, and saved paths.
   - Update `src/Carrot.Cli/Cli/CommandAppFactory.cs` to register `isearch`, add a description and
     concrete examples, and validate examples in the repository's supported development/test path.
   - Update `src/Carrot.Cli/Composition/ServiceRegistration.cs` for the command, workflow, and any
     shared resolver registrations without changing the interactive service graph.
   - Verify the scaffold compiles, production DI resolves the command, generated help shows all
     options and examples, and missing required options prevent command execution.

2. **Centralize noninteractive settings resolution without changing existing commands.**
   - Extend `src/Carrot.Cli/Configuration/RunSettingsResolver.cs` only as needed to resolve the
     Carrot endpoint and timeout for named iSearch categorization, preserving explicit-option then
     `CARROTCLI_ENDPOINT` precedence and the current configured timeout/default clustering behavior.
   - Reuse `ExcelOutputPathResolver` for both output options and add a focused collision check for
     original and categorized workbook paths. The resolver must not create directories or overwrite
     files during validation.
   - Resolve `--result-dataset` through `SearchReturnTypeCatalog.GetConfiguration()` and retain the
     selected definition's ordered `DefaultFields`; resolve `--database` only after authenticated
     live dataset discovery.
   - Keep validation side-effect-free. In particular, no health, datasets, search, Carrot, or
     workbook operation may occur when command relationships or local paths are invalid.
   - Extend `tests/Carrot.Cli.Tests/Configuration/RunSettingsResolverTests.cs` or add a focused
     settings resolver test for endpoint fallback, timeout boundaries, path collisions, required
     categorization output relationships, and case/whitespace handling.

3. **Implement the noninteractive iSearch workflow.**
   - Add the workflow implementation under `src/Carrot.Cli/ISearch/`, injecting only the role-specific
     abstractions it needs: `IISearchApiClient`, `ISearchOptionsValidator`, `SearchReturnTypeCatalog`,
     `ISearchResultsCategorizer`, the two existing Excel exporters, and the path/endpoint resolvers.
   - Validate User Secrets and return-dataset configuration before `GetHealthAsync`.
   - Call health, then datasets, and reject a database argument that is not present in the exact live
     dataset collection. Do not issue search when health, discovery, or database validation fails.
   - Build `SearchRequest` from the selected configured fields, trimmed query, `DefaultOp = "AND"`,
     and a row size no greater than 100. Preserve the selected database and return-dataset label in
     the result summary and export metadata where the existing contracts support it.
   - Create `SearchResultPageSession` from the first response and walk only the caller's bounded
     number of pages. Stop on complete cardinality, preserve accepted pages on later failure, and
     distinguish complete success from useful partial completion.
   - Save original output only after the search session has a valid accepted page. Run
     `ISearchResultsCategorizer` only when `--categorize` is present and records are loaded. Save
     categorized output only after categorization succeeds.
   - Ensure cancellation propagates through iSearch pacing, continuation, Carrot calls, and Excel
     writes; map it to `ExitCodes.Cancellation` without converting it into an ordinary failure.
   - Add intent-focused comments and logical vertical spacing for validation gates, page-loop
     limits, partial-state transitions, output ordering, and cancellation/cleanup paths.
   - Verify with `tests/Carrot.Cli.Tests/ISearch/SearchCommandWorkflowTests.cs` (or the nearest
     existing iSearch test file) using fake API, categorizer, exporter, and resolver boundaries.
     Cover health gating, live database validation, configured result-field selection, one-page and
     multi-page cursor walks, exact maximum-result retention, all-results completion, cap-induced
     partial results, cursor/total inconsistencies, no-record categorization prevention, successful
     categorization, output ordering, failure preservation, and cancellation.

4. **Implement the thin command adapter and automation-safe reporting.**
   - Complete `ISearchCommand.ExecuteAsync` so it translates settings to the workflow request,
     writes safe operation messages, writes the normal summary unless `--quiet` is set, and maps
     workflow status to the documented exit codes.
   - Do not print generic record JSON by default; named command output is intended for automation and
     file artifacts. Include database, result dataset, query completion, loaded/total counts,
     complete/partial state, categorization state, and absolute artifact paths.
   - Keep errors literal/escaped so query text, server messages, and paths cannot be interpreted as
     Spectre markup. Never include the API key or cookie in diagnostics.
   - Add all required XML documentation dividers, summaries, parameter/return/exception remarks,
     related `<seealso>` references, and `#region implementation` blocks to new or materially
     modified C# declarations. The command's XML documentation must accurately describe required
     options, no-prompt behavior, success/failure outcomes, and cancellation.
   - Verify with `tests/Carrot.Cli.Tests/Cli/ISearchCommandTests.cs` using
     `CommandAppTester`: help short-circuiting, valid binding, required-option failures, invalid
     option combinations, `--quiet`, exit-code mapping, no prompts, and safe output.

5. **Update internal help, README, and automation documentation with use cases.**
   - Update `src/Carrot.Cli/Docs/isearch.md` with a dedicated **Command-line usage** section. Document
     User Secrets prerequisites, the distinction between `--database` and `--result-dataset`, the
     required query, mutually exclusive `--max-results` and `--all-results` cursor-walk behavior,
     optional original/categorized workbook paths,
     `--categorize`, `--endpoint`, `--overwrite`, no prompts, exit codes, and at least the two
     search-only and search-plus-categorization PowerShell examples from this plan.
   - Update `src/Carrot.Cli/Docs/commands-options.md` so the command surface lists `isearch`, every
     option, validation relationship, output behavior, and a compact scheduler-friendly example.
   - Update `src/Carrot.Cli/Docs/getting-started.md` to explain when to use interactive iSearch versus
     named automation and link to the detailed iSearch topic.
   - Update `src/Carrot.Cli/Docs/output-columns.md` and `docs/output-format.md` to state that named
     iSearch output uses the existing `Results` worksheet, preserves accepted service-page order,
     and uses the existing categorized worksheet for Carrot memberships.
   - Update `docs/cli-reference.md`, `docs/task-scheduler.md`, `docs/troubleshooting.md`, and the
     root `README.md` with the command surface, safe User Secrets guidance, bounded and all-results
     cursor-walk behavior, common use cases, artifact paths, and the fact that named commands never
     prompt.
   - Preserve the embedded Markdown resource pattern in `Carrot.Cli.csproj`; the help topic already
     exists in `HelpTopicCatalog`, so update its content rather than adding a duplicate topic.
   - Add documentation/resource assertions to `tests/Carrot.Cli.Tests/Cli/HelpSystemTests.cs`,
     `CommandRouteTests.cs`, and any output documentation tests needed to verify that command help,
     `help isearch`, and redirected help expose the same examples and safety constraints. Do not put
     real credentials or live secret values in documentation, fixtures, or output snapshots.

6. **Verify the complete named-command feature.**
   - Run focused settings, API/workflow, exporter, command-routing, help, and documentation tests
     first.
   - Run `dotnet build .\Carrot-CLI.slnx --no-restore`.
   - Run `dotnet test .\Carrot-CLI.slnx --no-build --no-restore`.
   - Run `dotnet format .\Carrot-CLI.slnx --verify-no-changes --no-restore` and `git diff --check`.
   - Smoke-test generated help and expected failures without network access, for example:
     `carrot-cli isearch --help`, a missing-required-option invocation, a conflicting output-option
     invocation, and `carrot-cli help isearch`.
   - If live iSearch and Carrot smoke tests are authorized and the required User Secrets and service
     endpoints are available, run one harmless command with `--all-results` only when the returned
     dataset is known to remain within the hard retention ceiling; otherwise use a small
     `--max-results` value.
     Confirm the key is absent from console/log output and that both workbook paths are reported.
     Otherwise, report the live-service check as skipped; unit and fake-handler tests must not be
     treated as live verification.
   - Before implementation is declared complete, the coordinating agent must append one verified
     entry covering every changed source, test, configuration, and code-related documentation file
     to `C:\Source\Programs\Journal.md`. This pending plan is planning-only and does not itself
     require a journal entry.

## 8. Acceptance criteria

- `carrot-cli --help` lists `isearch`, and `carrot-cli isearch --help` documents every supported
  option and at least one valid example.
- The command never prompts and does not call iSearch, Carrot, or write files when required options,
  credential prerequisites, return-dataset configuration, endpoint settings, or output paths are
  invalid.
- The command validates the API key/contact configuration locally, calls authenticated health before
  dataset discovery, and searches only a database returned by live `GET /datasets`.
- `--result-dataset` resolves only configured `iSearchReturnTypes.Results` children, and its ordered
  `DefaultFields` are the only configured record fields sent in `fl`.
- The query is submitted with the selected live database, trimmed query text, `defaultOp=AND`, and
  no more than 100 rows per request. Cursor continuation preserves the original search context,
  walks every needed page, and retains no more than `--max-results` unless explicit `--all-results`
  mode is active.
- `--all-results` uses the service-reported `totalCount` and walks every cursor page until the loaded
  count equals that total; it does not require an arbitrary user-supplied maximum-result value.
- A complete walk returns success; a useful bounded partial walk is clearly reported and returns the
  documented partial-success code; iSearch failures, Carrot failures, output failures, and
  cancellation map to stable documented codes.
- `--output` writes the accepted original records through the existing atomic Excel framework, and
  `--categorize --categorized-output` sends accepted records through the existing Carrot path and
  writes the existing categorized workbook shape. No JSON sidecar or alternate workbook format is
  introduced.
- Original and categorized output paths are validated before writes, existing files require
  `--overwrite`, parent directories are not created implicitly, and path collisions are rejected.
- Categorization sends exactly the existing four iSearch source fields, performs no additional iSearch
  fetch, and retains source values and membership state in the categorized workbook.
- Console output and diagnostics never expose the iSearch API key, cookie, authorization headers,
  full payloads, or unbounded service error bodies. `--quiet` suppresses normal summaries only.
- Interactive iSearch behavior remains available and unchanged for existing users.
- New and materially modified C# code follows the repository's XML documentation, divider, naming,
  implementation-region, intent-comment, and spacing conventions. Focused tests, full build/test,
  format verification, whitespace checks, and applicable help/resource checks pass.
- The internal help files, README, CLI reference, Task Scheduler guidance, troubleshooting guidance,
  and output documentation contain usable search-only and search-plus-categorization examples with
  placeholder paths and no real secrets.

## 9. Risks and assumptions

- The requested “database” means the live iSearch dataset selected from `GET /datasets`; the
  requested “dataset result” means the configured return-dataset name under
  `iSearchReturnTypes.Results`. The command contract keeps these names separate to avoid sending a
  configuration label as the service database.
- The plan assumes the existing User Secrets values are available through the current project
  `UserSecretsId` and `Program` configuration pipeline. The repository must not gain a checked-in
  secret file or credential-bearing example.
- `--max-results` defaults to 100 records to keep a command safe when a scheduled invocation omits
  an explicit larger bound. `--all-results` is the explicit alternative when the caller wants the
  service total to control termination rather than guessing a value.
- The current Carrot categorization adapter uses configured default clustering selections after
  endpoint resolution. Exposing algorithm/language/template/parameter-file overrides is not required
  by the current request and remains a follow-up unless the existing contract requires it for a
  valid named invocation.
- iSearch database contents, field names, and response records remain service-owned. The plan uses
  live database discovery and configured field sets rather than hard-coding a database schema.
- A successful health response does not guarantee the later dataset or search request will succeed;
  each response remains independently validated and safely mapped.
- A bounded walk can produce useful accepted data without a complete dataset. The implementation
  must make that distinction explicit in both summary and exit code so downstream automation can
  choose whether partial output is acceptable.
- Live-service verification is optional and credential-dependent. Fake HTTP handlers and injected
  fakes are the required deterministic verification path.

## 10. Deferred follow-up

- Add resumable cursor checkpoints or restart support for long-running all-results walks.
- Add named field discovery, field-qualified query assistance, arbitrary validated `--field` input,
  saved-search execution, fetch-by-ID, or query-result formats other than the established Excel
  framework.
- Add named-command overrides for Carrot algorithm, language, template, and parameter files after
  a separate contract decision.
- Add resumable cursor checkpoints or persisted search sessions; the current command keeps state in
  memory for one invocation only.
- Add a machine-readable summary format such as JSON or CSV after a separate automation contract
  specifies schema stability and secret/record redaction requirements.

## 11. Skill usage

- `plan-software-changes`: used to ground the plan in existing repository symbols, define one
  coherent implementation phase, identify non-goals, and make every implementation step and
  acceptance criterion verifiable.
- `spectre-console-cli`: used for the typed `AsyncCommand<TSettings>` shape, declarative options,
  no-prompt validation, command registration, generated examples/help, cancellation, exit codes,
  DI, and `CommandAppTester` coverage.
- `isearch`: used for User Secrets authentication, cookie/header handling, health and dataset gates,
  live field configuration, cursor continuation, one-second pacing, bounded traversal, and safe
  response diagnostics.
- `dotnet-architectural-principles`: used to keep the command adapter, workflow orchestration, HTTP
  boundary, categorization boundary, and Excel persistence responsibilities separate and testable.
- `dotnet-automated-testing`: used for the unit/integration split, fake HTTP and service boundaries,
  command-parser tests, decision-table validation, page-limit boundaries, cancellation, and
  end-to-end use-case coverage.
- `verbose-code-documentation`: used to require complete XML documentation, separator headers,
  implementation regions, related references, and documentation-pipeline verification for new or
  materially modified C# members.
- `csharp-comment-spacing`: used to require intent-focused comments and logical vertical spacing for
  validation gates, cursor/page transitions, partial completion, cancellation, and output ordering.
