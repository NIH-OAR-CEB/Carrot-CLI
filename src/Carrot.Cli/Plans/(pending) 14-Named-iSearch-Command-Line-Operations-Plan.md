# Named iSearch Command-Line Operations

**Status:** Complete

## 1. Outcome

Add a prompt-free `isearch` command that runs the existing iSearch search, paging, optional Carrot
categorization, and Excel export workflow. The command must expose the advanced-query controls that
are now available in the interactive **Build Advanced Query** menu:

- base query (`q`)
- repeated query fields (`qf`)
- repeated field-qualified filters (`fq`)
- default Boolean operator (`defaultOp`)
- result-page size (`rows`)
- service update-date bounds (`updatedAfter` and `updatedBefore`)
- configured return fields (`fl`), selected by `--result-dataset`

The command must preserve the existing authenticated dataset-scoped GET search and cursor
continuation behavior. It must not expose a `sort` option: the latest iSearch work removed that
unsupported control from the shared request contract and interactive builder.

Example advanced invocation:

```powershell
carrot-cli isearch `
  --database grants `
  --result-dataset Grants `
  --query "*:*" `
  --query-field title `
  --query-field abstract `
  --filter-query "fy:2024" `
  --filter-query 'fundingCategory:"Research Project Grants"' `
  --default-op AND `
  --rows 100 `
  --updated-after 2024-10-01 `
  --updated-before 2025-09-30 `
  --max-results 300 `
  --output "C:\Results\grants-2024.xlsx" `
  --overwrite
```

The command also supports a simple query with `--query`, an explicit `--all-results` cursor walk,
and the existing optional Carrot categorization/output path. Authentication remains in User
Secrets (`iSearch:apiKey` and `iSearch:contactEmail`); no API-key option is added.

## 2. Problem

The command surface currently contains `process`, `preview`, `server-info`, `help`, and `about`,
while iSearch remains interactive. The interactive iSearch implementation now supports live field
discovery and advanced request construction, including `q`, `qf`, `fq`, `fl`, `defaultOp`, `rows`,
`updatedAfter`, and `updatedBefore`. The 2026-09-11 journal entries also record that the advanced
request contract, safe GET serialization, cursor preservation, tests, and documentation are
complete, and that `sort` was intentionally removed.

What remains is a named-command contract and workflow that can consume the completed advanced
request model without prompts. It must distinguish the live service database from the configured
return-dataset name, validate field-qualified options against the selected database when needed,
preserve all advanced values during cursor paging, and retain the existing categorization and
workbook contracts.

Relevant existing boundaries are:

- `src/Carrot.Cli/ISearch/Contracts/SearchRequest.cs` already models the JSON names and optional
  advanced controls.
- `src/Carrot.Cli/ISearch/ISearchApiClient.cs` already validates and URL-encodes those controls on
  the dataset-scoped GET operation and preserves them for continuation.
- `src/Carrot.Cli/ISearch/SearchResultPageSession.cs` owns accepted pages, records, cursors, totals,
  and the Excel-safe retention ceiling.
- `src/Carrot.Cli/Configuration/SearchReturnTypeCatalog.cs` resolves configured return fields for
  `fl`.
- `src/Carrot.Cli/ISearch/SearchResultsCategorizer.cs` and the two iSearch exporters are shared by
  the completed interactive categorization workflow.

## 3. Solution vision

Register one typed Spectre.Console.Cli leaf command named `isearch`. Its settings translate directly
to the shared `SearchRequest` contract while keeping CLI concerns separate from HTTP and reporting.
The command adapter will validate syntax and option relationships, then delegate to a focused
workflow that owns live discovery, request construction, paging, categorization, and export.

The named workflow will:

1. Validate local command relationships, credentials, return-dataset configuration, dates, output
   paths, and bounded/all-results selection before side effects.
2. Call authenticated `GET /health`, then `GET /datasets`, and accept only a database returned by
   live discovery.
3. Resolve `--result-dataset` from `iSearchReturnTypes.Results`; its ordered `DefaultFields` are
   the request's `fl` values.
4. When `--query-field` or `--filter-query` is supplied, call `GET /fields/{dataset}` and validate
   the referenced live field names. Do not hard-code a database schema. Filter values remain
   caller-supplied iSearch/Zulia expressions; the command validates their nonempty,
   field-qualified shape without attempting to replace the service query parser.
5. Build the existing `SearchRequest`: `q` defaults to `*:*` when omitted, `qf` and `fq` retain
   supplied order, `defaultOp` defaults to `AND`, `rows` defaults to 100, and date bounds are
   passed in exact `yyyy-MM-dd` form. The configured `fl` list is not replaced by CLI field options.
6. Submit the first GET search through `IISearchApiClient`, then use
   `SearchResultPageSession` for unchanged-request cursor continuation.
7. Retain either the requested bounded record count or every record through the service-reported
   total in explicit `--all-results` mode, subject to the existing hard retention ceiling.
8. Reuse the existing iSearch exporters and shared Carrot categorizer when requested, then emit a
   concise safe summary with counts, completion state, and absolute artifact paths.

The command must remain prompt-free. Interactive iSearch continues to own the guided builder,
pretty JSON review, typed filter help, and menu editing. The named command accepts the resulting
logical controls explicitly and relies on the shared API client for transport encoding.

## 4. Scope

- Add the named `isearch` command, typed settings, registration, generated help, and examples.
- Add command-line options for every supported advanced-query menu item:
  `--query`, repeatable `--query-field`, repeatable `--filter-query`, `--default-op`, `--rows`,
  `--updated-after`, and `--updated-before`.
- Reuse the completed `SearchRequest` advanced properties and `IISearchApiClient` serialization;
  do not duplicate or redesign those shared controls.
- Add live field discovery/validation for `qf` and `fq` references, while leaving query-expression
  semantics owned by iSearch/Zulia.
- Add noninteractive health, dataset, query, cursor-walk, optional categorization, and Excel
  orchestration with stable diagnostics and exit codes.
- Update embedded help, CLI reference, README, scheduler, troubleshooting, and output guidance
  with basic and advanced command examples.
- Add focused command/settings/workflow/help tests and run normal repository verification.

## 5. Non-goals

- Do not change the completed interactive advanced builder, basic query path, result pager, or
  interactive export/categorization prompts.
- Do not modify `SearchRequest` or `IISearchApiClient` advanced serialization unless implementation
  exposes a regression; add regression coverage instead of reimplementing completed behavior.
- Do not add `--sort`; the latest journal explicitly records that it is unsupported and removed.
- Do not add arbitrary `--field` result selection. `--result-dataset` remains the only source of
  configured `fl` fields.
- Do not add an API-key option, checked-in secrets, a second HTTP client, POST transport migration,
  saved searches, fetch-by-ID, query persistence, or a raw JSON editor.
- Do not silently parse or rewrite arbitrary filter syntax. The command accepts complete `fq`
  expressions and performs only safe boundary validation plus live field-name checking.
- Do not implicitly fetch every result page. Complete walking requires explicit `--all-results`.
- Do not introduce a new workbook format, JSON sidecar, or chunked categorization design.

## 6. Technical approach

### Command contract

Use a single `AsyncCommand<ISearchSettings>` registered as `isearch`. Repeated options must preserve
CLI order so the resulting `qf` and `fq` arrays are deterministic.

| Option | Contract | Shared request/behavior |
| --- | --- | --- |
| `--database <NAME>` | Required nonempty string | Live iSearch service dataset; must match `GET /datasets`. |
| `--result-dataset <NAME>` | Required nonempty string | Configured `iSearchReturnTypes.Results` child; its ordered `DefaultFields` become `fl`. |
| `--query <TEXT>` | Optional nonempty string after trimming; default `*:*` | Maps to `q`; blank input is rejected rather than treated as an omitted option. |
| `--query-field <FIELD>` | Repeatable live field name | Maps, in occurrence order, to `qf`; omitted when not supplied. |
| `--filter-query <EXPRESSION>` | Repeatable nonempty field-qualified iSearch/Zulia expression | Maps, in occurrence order, to `fq`; values are not re-authored by the CLI. |
| `--default-op <AND\|OR>` | Optional enum-like value; default `AND` | Maps to `defaultOp`; reject undefined or case-ambiguous values according to repository conventions. |
| `--rows <COUNT>` | Optional integer from 1 through 100; default 100 | Maps to `rows`, independent of retained `--max-results`. |
| `--updated-after <YYYY-MM-DD>` | Optional exact ISO date | Maps to `updatedAfter`; validate parseability without converting the submitted text to a timestamp. |
| `--updated-before <YYYY-MM-DD>` | Optional exact ISO date | Maps to `updatedBefore`; reject a lower bound after the upper bound. |
| `--max-results <COUNT>` | Positive retained-record bound, default 100 when all-results is absent | Stops after the accepted prefix reaches the bound; it does not change the service page size. |
| `--all-results` | Boolean flag, mutually exclusive with explicit `--max-results` | Uses the first response's stable `totalCount` and walks every required cursor page. |
| `--output <PATH>` | Optional existing-parent `.xlsx` path | Saves accepted original records through `SearchResultsExporter`. |
| `--categorize` | Boolean flag | Categorizes accepted records through `ISearchResultsCategorizer`. |
| `--categorized-output <PATH>` | Optional existing-parent `.xlsx` path | Requires `--categorize`; saves through `CategorizedISearchResultsExporter`. |
| `--endpoint <URI>` | Optional Carrot endpoint | Uses existing explicit-option/environment precedence when categorization is requested. |
| `--timeout-seconds <SECONDS>` | Optional positive integer | Reuses existing Carrot timeout resolution for categorization. |
| `--overwrite` | Boolean flag | Required to replace an existing requested workbook. |
| `--quiet` | Boolean flag | Suppresses normal success summary but not warnings/errors. |

`--query-field` and `--filter-query` are the command-line counterparts of the interactive
**Select Query Fields** and **Add Filter** actions. A filter example such as
`--filter-query 'fundingCategory:"Research Project Grants"'` is already a complete `fq` value;
numeric, Boolean, phrase, and date/range syntax remains governed by the live iSearch/Zulia contract.
The command must not offer a misleading type-specific prompt or invent a second filter language.

The existing interactive request contract is authoritative for JSON names and omission rules:
`dataset`, `q`, optional `qf`, optional `fq`, `fl`, `rows`, `defaultOp`, optional `updatedBefore`,
and optional `updatedAfter`. The named command should construct the same `SearchRequest` object and
let the existing client encode each complete query-string value. Cursor is transport state and is
never a user option.

### Validation and live field discovery

Use Spectre settings validation for required values, scalar ranges, date syntax, option conflicts,
and categorization/output relationships. Use the workflow boundary for configuration and live-state
validation. No iSearch, Carrot, or workbook side effect may occur when local validation fails.

When qf or fq values are present, discover fields only after health and database validation. Match
query-field names exactly against the returned nonempty `SearchField.Name` values. For each filter,
validate that its leading field identifier is present in the live schema and that the expression is
nonempty; preserve the full expression after validation. Do not hard-code fiscal-year, category, or
other field names. If a filter cannot be safely identified as field-qualified, return a clear local
validation failure rather than guessing.

The command may use `--query "*:*"` explicitly, or omit `--query` to obtain the same match-all
default used by the interactive builder. A supplied `--query` is trimmed once and then preserved.
Dates remain strings in the request so the API receives the documented calendar representation.

### Paging, retention, and completion

Create `SearchResultPageSession` from the first successful response. Every continuation must use
the session's unchanged `SearchRequest` and add only the current service cursor. In bounded mode,
retain no more than `--max-results`; if the bound falls inside a service page, retain the accepted
prefix without requesting another page. Report useful bounded data as partial when more service
pages remain. In all-results mode, continue until the initial `totalCount` is accepted.

Honor the session's existing checks for changing totals, empty/unchanged cursors, invalid page
metadata, cancellation, and the 1,048,575-row Excel-safe ceiling. An all-results run that cannot
reach the stable total is a failure and must not be labeled complete. The selected `--rows` value
controls service page size only; it must not weaken retention or completion guards.

### Categorization and output

Run categorization only over accepted records already retained in the session. Reuse the completed
adapter that submits exactly `nihApplId`, `title`, `abstract`, and `specificAims`, including its
normalization of numeric/null/missing values. Resolve the Carrot endpoint and timeout through the
existing settings boundary and preserve configured clustering defaults.

Resolve both workbook paths before network work, require `.xlsx` and existing parent directories,
reject collisions, and pass `--overwrite` to the existing atomic writer. Write original output in
service order and categorized output in the existing categorized worksheet shape. Do not write an
artifact after a failed prerequisite or claim that a bounded artifact is complete.

### Exit codes and diagnostics

Preserve existing codes where possible: `0` success, `1` local validation/configuration failure,
`2` useful bounded partial completion, `4` Carrot endpoint/list failure, `5` Carrot categorization
failure, `6` workbook failure, and `130` cancellation. Add a dedicated iSearch failure code only if
the existing code set cannot distinguish health/discovery/search/cursor failures.

Use `OperationResult` and `OperationMessage`. Diagnostics may include option names, bounded service
messages, counts, and paths, but never API keys, cookies, authorization headers, record payloads,
or unbounded response bodies. Escape literal query/filter text when rendering through Spectre.

### Alternatives considered

- **Reuse the interactive builder directly:** rejected because it prompts and owns terminal state;
  reuse its shared request contract and API boundary instead.
- **Add a second command for advanced searches:** rejected because basic and advanced requests have
  one paging/export pipeline and do not need separate persisted contracts.
- **Expose raw JSON:** rejected because it bypasses option validation and creates a second public
  request language.
- **Accept arbitrary result fields:** rejected because configured return datasets already own `fl`.
- **Create a new iSearch serializer:** rejected because the completed client already safely encodes
  qf, fq, dates, rows, operator, fields, and cursor continuation.

## 7. Implementation steps

1. **Add the settings and command registration scaffold.**
   - Add `src/Carrot.Cli/Cli/Settings/ISearchSettings.cs` with the complete option set above,
     repeated ordered collections, defaults, descriptions, and deterministic relationship validation.
   - Add `src/Carrot.Cli/Cli/Commands/ISearchCommand.cs` as a thin `AsyncCommand<ISearchSettings>`.
   - Register the command and examples in `src/Carrot.Cli/Cli/CommandAppFactory.cs`; register its
     dependencies in `src/Carrot.Cli/Composition/ServiceRegistration.cs` without disturbing the
     interactive graph.
   - Verify help shows every advanced option and no `sort` option, and invalid scalar combinations
     stop before workflow execution.

2. **Add the named request/workflow boundary.**
   - Add focused request/result/workflow contracts under `src/Carrot.Cli/ISearch/`, for example
     `SearchCommandRequest.cs`, `SearchCommandResult.cs`, and `ISearchCommandWorkflow.cs`.
   - Carry q, ordered qf/fq, operator, rows, dates, selected database/return dataset, paging mode,
     output settings, and resolved Carrot settings without duplicating `SearchRequest` semantics.
   - Keep command parsing/reporting separate from iSearch HTTP, paging, categorization, and Excel.

3. **Resolve settings and advanced field references.**
   - Extend `RunSettingsResolver` only if needed for existing endpoint/timeout precedence; do not
     make unrelated clustering options mandatory for a search-only invocation.
   - Reuse `SearchReturnTypeCatalog` and `ExcelOutputPathResolver`; validate output collisions and
     overwrite policy without creating directories or files.
   - After health and live database validation, call `GetFieldsAsync` when qf/fq options require it,
     then validate exact live field references and preserve filter expressions.
   - Add focused resolver/workflow tests for defaults, date boundaries/order, repeated option order,
     field failures, filter-shape failures, path collisions, and side-effect-free rejection.

4. **Implement search, paging, categorization, and export orchestration.**
   - Call `ISearchOptionsValidator`, health, datasets, configured return-dataset resolution, and
     search in that order; never search an undiscovered database.
   - Build the completed `SearchRequest` with `*:*`/AND/100 defaults and all supplied advanced values;
     let `IISearchApiClient` perform the established GET serialization and authentication safeguards.
   - Use `SearchResultPageSession` for bounded or explicit all-results cursor walking, preserving
     exact bounded prefixes and structured partial/failure state.
   - Reuse both existing exporters and `ISearchResultsCategorizer`, forwarding cancellation through
     every boundary.
   - Cover health gating, live database/field validation, exact request construction, advanced-value
     continuation preservation, rows/max-results interaction, all-results completion, partial walks,
     no-record categorization, output ordering, failures, and cancellation with injected fakes.

5. **Complete the command adapter and safe reporting.**
   - Translate settings to the workflow request, write structured messages, honor `--quiet`, and
     return the documented exit code.
   - Report database, return dataset, advanced-query completion, loaded/total counts, partial state,
     categorization state, and absolute artifact paths without printing records or credentials.
   - Add XML documentation, divider headers, implementation regions, intent comments, and spacing to
     all new or materially modified C# declarations under repository conventions.
   - Test with `CommandAppTester`: help short-circuiting, required options, repeated values, defaults,
     dates, qf/fq validation, conflicts, no prompts, safe diagnostics, quiet output, exit codes,
     and cancellation.

6. **Update documentation and verify the feature.**
   - Update `src/Carrot.Cli/Docs/isearch.md`, `src/Carrot.Cli/Docs/commands-options.md`,
     `src/Carrot.Cli/Docs/getting-started.md`, `docs/cli-reference.md`, `docs/task-scheduler.md`,
     `docs/troubleshooting.md`, `README.md`, `src/Carrot.Cli/Docs/output-columns.md`, and
     `docs/output-format.md` with basic and advanced named-command examples.
   - Document `--query-field`/`--filter-query` repetition and ordering, `--default-op`, `--rows`,
     date formats, `--result-dataset`/`fl`, `--max-results` versus `--all-results`, no prompts, and
     the absence of `--sort`. Keep secrets and live payloads out of resources and examples.
   - Add help/resource assertions and run focused tests, `dotnet build .\Carrot-CLI.slnx --no-restore`,
     `dotnet test .\Carrot-CLI.slnx --no-build --no-restore`,
     `dotnet format .\Carrot-CLI.slnx --verify-no-changes --no-restore`, and `git diff --check`.
   - Smoke-test root/leaf help and expected local validation failures without network access. Treat
     live iSearch/Carrot verification as optional and credential-dependent.

## 8. Acceptance criteria

- `carrot-cli isearch --help` lists every supported advanced option, repeated-option behavior, and a
  valid advanced example; it does not list `--sort`.
- The command never prompts and performs no network, Carrot, or file side effect when local options,
  credentials, configured return dataset, dates, field references, endpoint, or output paths fail.
- Omitted `--query`, `--default-op`, and `--rows` produce `q=*:*`, `defaultOp=AND`, and `rows=100`;
  explicit values are trimmed/validated according to the contract.
- Repeated `--query-field` and `--filter-query` options preserve occurrence order and map to qf/fq.
  qf names and filter-leading field names are validated against live `GET /fields/{database}` data.
- `--updated-after` and `--updated-before` accept exact `yyyy-MM-dd` values, reject malformed or
  reversed ranges, and serialize unchanged; filter expression syntax is not silently rewritten.
- `--result-dataset` resolves configured ordered `DefaultFields` as the only `fl` values. Advanced
  values remain unchanged in every cursor continuation request.
- The command uses authenticated health, live database discovery, the existing GET serializer, and
  the existing one-second pacing/credential protections; no credential appears in URI or diagnostics.
- Bounded mode retains no more than `--max-results` records and reports a useful incomplete walk as
  partial. Explicit `--all-results` reaches the stable service total or fails without claiming
  completion, subject to the existing retention ceiling.
- Original and categorized workbooks use the existing atomic exporters, preserve service order and
  categorized membership semantics, require `--overwrite` for replacement, and reject path collisions.
- Categorization sends only the existing four iSearch source fields and performs no additional fetch.
- Stable exit codes, quiet behavior, cancellation, bounded diagnostics, interactive compatibility,
  focused tests, build, test, format, whitespace, and help/resource checks pass.

## 9. Risks and assumptions

- 'Database' means the live iSearch dataset; 'result dataset' means a configured
  `iSearchReturnTypes.Results` child. They remain separate command options.
- Live field metadata is authoritative. A syntactically field-qualified expression may still be
  rejected by iSearch; the API client's bounded sanitized failure remains authoritative.
- The command accepts complete fq expressions because duplicating the interactive type-aware
  formatter would create a second query language. Documentation must provide safe examples and link
  users to the iSearch/Zulia syntax guidance.
- `rows` controls one service page while `max-results` controls retained records. A cap can fall in
  the middle of a page and must be projected without another request.
- User Secrets and live-service verification may be unavailable in CI; fake handlers and injected
  boundaries are the deterministic required checks.
- Existing Carrot defaults remain sufficient for named categorization. Algorithm/language/template/
  parameter-file options remain outside this plan.

## 10. Deferred follow-up

- Resumable cursor checkpoints, persisted search sessions, saved searches, fetch-by-ID, or a machine-
  readable output schema.
- Arbitrary configured result-field editing, query autocomplete, query-language linting, schema cache,
  or a raw JSON editor.
- A separate typed CLI filter language that reproduces the interactive field-type formatter.
- Named Carrot algorithm/language/template/parameter overrides and chunked categorization.
- POST transport migration or any reintroduction of the unsupported sort control.

## 11. Plan evidence and skill usage

- `src/Carrot.Cli/Plans/(done) 15-Interactive-iSearch-Advanced-Query-Builder-Plan.md` is the source
  for the completed q/qf/fq/fl/defaultOp/rows/date semantics, live-field guidance, and GET transport.
- The 2026-09-11 journal entries confirm that the shared advanced request/API/interactive behavior,
  tests, and documentation are implemented, and that sort was removed.
- `plan-software-changes`: used to keep this as one repository-grounded implementation phase with
  explicit scope, non-goals, steps, and observable acceptance criteria.
- `spectre-console-cli`: used for typed settings, repeated options, validation lifecycle, DI,
  generated help, cancellation, and `CommandAppTester` coverage.
- `isearch`: used for live dataset/field discovery, authenticated GET constraints, q/qf/fq/fl/date
  semantics, array encoding, paging, and service-owned filter syntax.
- `dotnet-automated-testing`: used for parser, workflow, fake-handler, boundary, cancellation, and
  end-to-end command coverage.
- `verbose-code-documentation` and `csharp-comment-spacing`: applied to the new and materially
  modified C# command, workflow, settings, reporting, paging, and test declarations.

## 12. Implementation record

- The named `isearch` command, advanced settings, workflow, bounded/all-results paging, live field
  validation, optional categorization, Excel export, reporting, registration, tests, and documentation
  updates are implemented in the repository.
- Verification completed with focused tests, the full solution test suite, solution build, formatting
  verification, generated leaf help, local option-conflict smoke testing, and whitespace validation.
