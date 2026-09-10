# iSearch Fetch All Pages

Status: Pending

## 1. Outcome

Add a **Fetch All Pages** action immediately below **Fetch Next Result Page** in the interactive iSearch results menu. When selected, the action sequentially follows the current query's cursor through every remaining service data page, appends each successfully validated page to the existing in-memory session, and leaves the operator at the final fetched page.

While the walk is running, the **Search Summary** must remain visible as a live status view. Its **Data Page** value must advance after each accepted response, and its progress bar must show the actual proportion of loaded records (`loaded records / totalCount`), including the final partial page. The operation must stop at the service end conditions, fail safely on an incomplete or contradictory walk, propagate cancellation, and never bypass the documented authenticated iSearch request throttle.

## 2. Problem

The completed one-page-fetching and Excel-export work provides the required foundation but intentionally leaves automated walking out of scope:

- `src/Carrot.Cli/ISearch/SearchResultPageSession.cs` owns the stable `SearchRequest`, current response, cursor continuation, accumulated `WalkedPages`, and accumulated `WalkedResults`, but exposes only `FetchNextAsync`, which advances one page per caller action.
- `src/Carrot.Cli/Cli/UI/SearchResultsPager.cs` offers **Fetch Next Result Page** and updates the displayed response only after that one transition. There is no action that continues until the service result set is exhausted.
- `src/Carrot.Cli/Cli/UI/ApplicationFooterState.cs` and `ApplicationFooterRenderer.cs` distinguish **Data Page** from **Display Page**, but the summary currently has no loaded-record progress state and cannot be updated as a multi-page operation proceeds.
- `src/Carrot.Cli/ISearch/ISearchApiClient.cs` already serializes authenticated requests through `waitForRequestSlotAsync`, using the configured `MinimumRequestIntervalMilliseconds` (1,000 ms in `src/Carrot.Cli/appsettings.json`) and honoring server `Retry-After` values for transient retries. A new loop must use this boundary rather than issuing requests directly or in parallel.
- `src/Carrot.Cli/Docs/isearch.md` currently explains that the operator must fetch pages one at a time and explicitly says that the CLI does not automatically fetch every page.

Without a session-owned loop, the pager would have to duplicate cursor, cardinality, termination, failure-retention, and accumulation rules. Without a live progress state, the operator would not know how far a long, throttled walk has actually progressed.

## 3. Solution vision

Keep the iSearch API client responsible for one authenticated, paced continuation request and keep the session responsible for the state machine that walks those requests. Add a `FetchAllAsync`-style operation to `SearchResultPageSession` that repeatedly invokes the existing one-page transition, never changes the stable query context, and reports progress only after a complete page has been validated and adopted.

The session will treat the initial successful response as already loaded. For each remaining page it will:

1. Confirm that the current cursor/cardinality state permits continuation.
2. Call `SearchNextPageAsync` through the existing client boundary, which applies the shared sequential throttle, timeout, retry policy, redirect refusal, response-size limit, and cancellation behavior.
3. Append the accepted page exactly once and update `CurrentPage`.
4. Publish a progress snapshot containing the new service page number, total service pages, loaded-record count, total-record count, and percentage.
5. Stop only when the loaded records reach `totalCount`, the cursor is empty, a zero-record terminal response is received, or the service/cardinality contract reports no next page. An unchanged cursor or contradictory response must not create an infinite loop or a false 100% result.

The pager will add **Fetch All Pages** directly after **Fetch Next Result Page**, only while another service page is available. It will run the session operation inside a single live summary render target. The target will show the current **Data Page X of Y**, current chunk count, total loaded records, and a bounded bar with an explicit percentage. The terminal display lines remain presentation-only: they are not re-rendered or used to calculate fetch progress until the all-pages operation completes.

When the walk finishes successfully, the pager will adopt the session's final `CurrentPage`, rebuild only that page's display lines, reset the terminal display-page index to its first page, and return to the normal selection menu. If a later page fails, pages already accepted remain in the session, the summary reports the actual partial percentage, the safe operation message is displayed, and the user can retry **Fetch All Pages** or use **Fetch Next Result Page**. Caller cancellation remains cancellation rather than an ordinary failure.

The progress calculation is record-based rather than page-based. For example, 250 loaded records out of a 275-record result set displays 90.9%, even though the operator is on page 3 of 3; this avoids overstating completion for a partial final page. Render nonterminal percentages to one decimal place using invariant formatting, and display exactly 100% only after completion. An empty successful result set displays 100% loaded with no fetch action because there is no data to walk.

## 4. Scope

- Add a reusable sequential all-pages operation to the existing iSearch result session.
- Preserve the current query, selected live database, configured return dataset, ordered fields, row limit, cursor semantics, result accumulation, Excel-safe record limit, failure retention, and cancellation behavior.
- Add page-walk progress snapshots and live **Search Summary** rendering with actual loaded-record percentage.
- Add **Fetch All Pages** below **Fetch Next Result Page** without conflating service-page navigation with terminal display-page navigation.
- Ensure every continuation request uses the existing authenticated iSearch throttle and transient `Retry-After` handling.
- Add focused xUnit coverage for successful walks, partial progress, terminal conditions, failures, cancellation, cursor safety, throttling delegation, menu ordering, and live-summary state.
- Update iSearch help, getting-started, privacy, troubleshooting, output, and CLI-reference text only where it describes the new explicit all-pages action and its data-handling behavior.

## 5. Non-goals

- Do not make the initial search automatically fetch all pages.
- Do not fetch pages when the operator chooses **Save iSearch Results to Excel**; save continues to write only the pages already walked.
- Do not fetch pages in parallel, add offset pagination, bypass the typed API client, or add a second throttle in the pager.
- Do not change the iSearch endpoint, authentication cookie, query syntax, selected return-dataset workflow, configured field set, row limit, or Excel workbook schema.
- Do not add background execution, scheduling, resumable checkpoints, persisted crawl state, cross-query aggregation, or a noninteractive crawl command.
- Do not use terminal display-page counts to determine service completion or progress.
- Do not silently truncate a walk at the existing Excel-safe retained-result limit; report a bounded failure before adopting an over-limit page.
- Do not claim a successful 100% walk when the service returns an early empty page, unchanged cursor, inconsistent total, or other evidence that the result set was not fully loaded.

## 6. Technical approach

### Session state machine and result contract

Extend `SearchResultPageSession` with a session-owned all-pages method and a small immutable progress value, such as `SearchPageWalkProgress`. The final names should match the repository's internal naming conventions. The progress value should expose at least:

- current service data page and total service pages;
- loaded record count and service total record count;
- a percentage in the inclusive range 0–100; and
- whether the snapshot represents a completed walk or an expected incomplete/failure state.

The all-pages method should report an `OperationResult` because HTTP failure, malformed continuation data, a contradictory cardinality, an unchanged cursor, and the retained-result limit are expected operational outcomes. It should not hide accepted partial progress: a failed operation leaves all pages accepted before the failure in `WalkedPages`/`WalkedResults` and leaves `CurrentPage` at the last accepted page. Cancellation is rethrown through the existing client/session behavior so the application retains its standard cancellation exit code.

The method must not mutate state before `FetchNextAsync` has returned a validated success. It should use the current cursor exactly once per iteration and publish progress after the corresponding page has been appended. A progress callback must be invoked synchronously with state publication or through an explicit asynchronous callback contract so the pager cannot observe a page number that does not yet match the retained records.

Use the initial response's `TotalResults` as the progress denominator for the query visit. Later responses must be checked for compatible `TotalResults`; if the service changes the total in a way that makes completion ambiguous, return a safe failure and do not adopt that contradictory page. Loaded counts must not be allowed to exceed the denominator without an explicit service-contract decision. Retain the existing 1,048,575-data-row guard and reject the next page before mutating the aggregate when it would exceed that limit.

Use all documented stop conditions, not only `PageNumber < TotalPages`: loaded records reaching `TotalResults`, a blank cursor, a zero-record response, an unchanged cursor, and exhausted cardinality must all terminate or fail safely as appropriate. A blank cursor after the loaded count reaches the total is successful completion; a blank/unchanged cursor or empty page before the total is loaded is an incomplete-walk failure with the true partial percentage.

### Throttle and retries

The all-pages loop must call only `SearchResultPageSession.FetchNextAsync`; it must never call `HttpClient`, build a URI, add a cursor, delay independently, or schedule concurrent requests. Each continuation therefore passes through `ISearchApiClient.sendAsync` and `waitForRequestSlotAsync`, whose shared gate spaces request starts using `MinimumRequestIntervalMilliseconds`. The production setting remains the documented authenticated one-second interval in `appsettings.json`; transient HTTP retries continue to honor a valid `Retry-After` value and remain bounded by `TransientRetryCount`.

Do not use a UI progress refresh interval as a network interval. Live rendering may refresh after a page response is adopted, while the API client alone determines when the next request may start. Add a deterministic test seam only if needed to observe timing; do not weaken the production throttle to make tests fast. Existing unit tests may continue to use a zero interval only in explicitly isolated fake-client tests that do not claim to verify public-service pacing.

### Live Search Summary

Extend `ApplicationFooterState` with loaded-record/progress fields that are optional for non-iSearch views. Update `ApplicationFooterRenderer` so the existing Search Summary panel includes a bounded, literal progress bar and percentage when progress data is present. The bar should be safe for narrow terminals, should not interpret dataset or service values as markup, and should display `100%` only for a completed or empty result set.

Use the installed `Spectre.Console` 0.55.0 live-display API (verify the exact signatures against the resolved assembly) to update one summary render target after each successful page. Keep the summary renderer responsible for layout and the session responsible for data/progress values. The live target should include the latest **Data Page** number while the request loop is in progress and should stop cleanly before the normal `SelectionPrompt` is shown again. Avoid printing a new full summary for every page or leaving a live display active while a prompt owns the terminal.

During the network wait for the next page, keep the last committed counts and show a literal status such as `Fetching data page N of Y...`; after adoption, replace it with the new page and percentage. On expected failure, close the live target, show the sanitized operation message, and return to the pager with the retained partial state. On cancellation, close/dispose the live target in a `finally` path and rethrow cancellation.

### Interactive menu behavior

Add a `FetchAllPages` choice to `SearchResultsPager.PageChoice` immediately after `FetchNextResultPage` and before the existing save action. Its label must be exactly **Fetch All Pages**. Show it only when `session.CanFetchNextPage` is true; once the final page is loaded it disappears along with the one-page fetch action. The choice must not alter the configured return dataset or re-run the query.

The pager should retain the existing local display `lines`, `pageIndex`, and `pageCount` until the walk completes. After each accepted page, the live summary uses session state; when the operation returns, the pager refreshes its normal display from the final current response and resets only `pageIndex`. If a partial failure occurs, refresh from the last accepted response and keep both fetch actions available when the cursor remains usable. The existing save action must continue to receive the same session and therefore export every page accepted before or during the completed walk.

### Alternatives considered

- **Loop directly in `SearchResultsPager`:** rejected because UI code would own cursor termination, result accumulation, and failure invariants.
- **Add a new bulk endpoint or bulk API-client method that bypasses `FetchNextAsync`:** rejected because it would duplicate request construction and risk bypassing the established pacing/retry boundary.
- **Run one task per page:** rejected because concurrent requests violate the documented sequential throttle and cursor order.
- **Use `current page / total pages` as progress:** rejected because it overstates completion on a partial final page and does not represent actual loaded records.
- **Use a separate progress screen below the summary:** rejected because the requirement is for the Search Summary to communicate both Data Page state and loaded percentage.
- **Continue after an empty or unchanged-cursor response:** rejected because it can loop or misrepresent an incomplete result set as complete.

## 7. Implementation steps

1. **Confirm the existing contracts and define walk/progress invariants.**
   - Files/symbols: `src/Carrot.Cli/ISearch/SearchResultPageSession.cs`, `src/Carrot.Cli/ISearch/ISearchApiClient.cs`, `src/Carrot.Cli/ISearch/Contracts/SearchResponse.cs`, `src/Carrot.Cli/ISearch/Contracts/SearchCardinality.cs`, `src/Carrot.Cli/Configuration/ISearchOptions.cs`, `src/Carrot.Cli/appsettings.json`.
   - Change: document the distinction between one-page continuation and all-pages orchestration; define `loaded/total` percentage semantics, empty-result semantics, early-terminal behavior, unchanged-cursor behavior, total-count consistency, and the existing retained-result capacity.
   - Why: the progress denominator and termination rules must be stable before UI rendering or tests are added.
   - Verify: compare each rule with the iSearch skill's documented cursor contract (`cursor`, `returnedCount`, `totalCount`, `results`, zero/unchanged cursor stops), the current response parser, and the existing one-second production setting. Do not make a live request without configured User Secrets.

2. **Add the session-owned all-pages transition and progress contract.**
   - Files/symbols: new `src/Carrot.Cli/ISearch/SearchPageWalkProgress.cs` (or an equivalent focused internal contract), `src/Carrot.Cli/ISearch/SearchResultPageSession.cs`, and `tests/Carrot.Cli.Tests/ISearch/SearchResultPageSessionTests.cs`.
   - Change: implement a sequential `FetchAllAsync` operation over the existing `FetchNextAsync`; publish an initial/after-page progress snapshot; preserve exact service-page and record order; stop at complete cardinality/cursor conditions; detect unchanged cursors and early empty pages; reject contradictory totals and over-limit pages before state mutation; retain accepted partial state on expected failure.
   - Why: session state is the single owner of cursor transitions and accumulation, allowing the pager to remain a presentation consumer.
   - Verify: use fake responses for page sequences such as 100/100/75 records; assert progress `40%`, `80%`, `100%` for a 275-record total; assert page numbers `1`, `2`, `3`; test already-complete/no-op behavior, blank cursor, zero-record initial response, early empty response, unchanged cursor, changed total, page-limit overflow, failed page N, retry after failure, and cancellation.

3. **Add deterministic pacing coverage without duplicating throttle logic.**
   - Files/symbols: `src/Carrot.Cli/ISearch/ISearchApiClient.cs` only if a test seam or stale contract comment needs adjustment; `tests/Carrot.Cli.Tests/ISearch/ISearchApiClientTests.cs`; `tests/Carrot.Cli.Tests/ISearch/SearchResultPageSessionTests.cs`.
   - Change: keep all-pages callers on `SearchNextPageAsync`; add or refine tests proving sequential continuation calls preserve cursor order, use the same request context, and pass through the client’s configured interval and `Retry-After` behavior. Do not add a session-level `Task.Delay`.
   - Why: a second delay can make the walk unnecessarily slow, while direct calls could violate the documented one-second authenticated request start interval.
   - Verify: with a controlled timing seam or narrowly bounded real delay, assert no overlapping continuation calls, request starts are at least the configured interval apart, `Retry-After` remains honored, and cancellation interrupts pacing. Keep tests that use zero delay clearly marked as fake-client state tests rather than throttle tests.

4. **Render loaded-record progress inside the Search Summary.**
   - Files/symbols: `src/Carrot.Cli/Cli/UI/ApplicationFooterState.cs`, `src/Carrot.Cli/Cli/UI/ApplicationFooterRenderer.cs`, `src/Carrot.Cli/Cli/UI/SearchResultsPager.cs`, and focused tests in `tests/Carrot.Cli.Tests/ISearch/SearchResultsPagerTests.cs` plus a new `tests/Carrot.Cli.Tests/Cli/ApplicationFooterRendererTests.cs` if isolated renderer coverage is useful.
   - Change: add nullable progress state to the shared footer, render a bounded bar and literal percentage in the Search Summary, and update the summary target after every accepted page through `Spectre.Console` 0.55.0 live display support. Keep Data Page and Display Page separate and keep the terminal prompt outside the live region.
   - Why: the user needs current service-page and actual data-loaded state while throttled requests are in flight, without confusing terminal line navigation with service paging.
   - Verify: render initial, intermediate, final, empty, narrow-terminal, and partial-failure states; assert the output contains the expected Data Page values and percentages, never reports a percentage above 100, and does not treat dynamic values as markup. Verify the live display is closed on success, expected failure, and cancellation.

5. **Integrate the Fetch All Pages menu action.**
   - Files/symbols: `src/Carrot.Cli/Cli/UI/SearchResultsPager.cs`, `src/Carrot.Cli/Cli/UI/ISearchResultsPager.cs` only if its contract changes, `tests/Carrot.Cli.Tests/ISearch/SearchResultsPagerTests.cs`, and `tests/Carrot.Cli.Tests/ISearch/InteractiveISearchFlowTests.cs`.
   - Change: add **Fetch All Pages** immediately after **Fetch Next Result Page**; invoke the session loop with the live-summary callback; update the displayed response and service Data Page after completion; retain the latest accepted page and retry choices after partial failure; preserve the save action and selected return dataset.
   - Why: the pager is the correct interaction boundary, while the session remains the source of truth for what has been loaded.
   - Verify: test the exact choice ordering, absence before a successful query and after terminal completion, a walk starting from page 1, a walk resumed after manual page 2 fetching, no additional fetch from display-page navigation, final display reset, page-1/page-2/page-3 accumulation, save visibility after the walk, selected database/return-dataset retention, expected failure recovery, and cancellation propagation.

6. **Review composition and shared UI compatibility.**
   - Files/symbols: `src/Carrot.Cli/Composition/ServiceRegistration.cs` and any new progress contract registration only if the chosen implementation requires DI; existing `tests/Carrot.Cli.Tests/Cli/InteractiveMenuTests.cs` and composition-focused tests.
   - Change: prefer the current transient pager and singleton shared renderer registration; register no new API client or parallel fetch service. Add DI wiring only for a genuinely injected progress abstraction or clock/test seam.
   - Why: the all-pages action is a behavior extension of the existing graph, not a new subsystem.
   - Verify: resolve the production interactive graph, confirm existing processed-results and iSearch Excel export services still resolve, and verify the main menu/footer remains unchanged when no iSearch result view is active.

7. **Update operator documentation and privacy guidance.**
   - Files/symbols: `src/Carrot.Cli/Docs/isearch.md`, `src/Carrot.Cli/Docs/getting-started.md`, `src/Carrot.Cli/Docs/privacy.md`, `src/Carrot.Cli/Docs/troubleshooting.md`, `src/Carrot.Cli/Docs/output-columns.md`, `docs/cli-reference.md`, and `README.md` only where iSearch behavior is summarized.
   - Change: describe **Fetch All Pages** as an explicit action below **Fetch Next Result Page**; explain that it fetches only remaining pages for the current query, updates Data Page numbers, reports loaded-record percentage, follows the one-second authenticated throttle and bounded retries, preserves partial successful pages after failure, and does not change Save's already-walked-page scope. Update the stale claim that the CLI never automatically fetches every page. State that the walk remains in memory for the current visit and may contain sensitive returned research data.
   - Why: interactive help and privacy text must match the new network and memory behavior.
   - Verify: embedded help/resource tests pass; search for stale wording such as “does not automatically fetch every page” or “only one page at a time” and revise only claims contradicted by this explicit action; preserve accurate statements about save, credentials, sidecars, and logs.

8. **Run focused and full verification.**
   - Run focused session, API-client pacing, footer/live-render, pager, interactive-flow, composition, and documentation tests first.
   - Run `dotnet build .\\Carrot-CLI.slnx --no-restore --disable-build-servers -m:1 --verbosity minimal`, `dotnet test .\\Carrot-CLI.slnx --no-build --no-restore --verbosity normal`, `dotnet format .\\Carrot-CLI.slnx --verify-no-changes --no-restore` when applicable, and `git diff --check`.
   - Confirm `src/Carrot.Cli/Carrot.Cli.csproj` continues to emit XML documentation and that all materially modified C# types, methods, properties, and tests retain the repository's XML divider, `#region implementation`, and intent-comment conventions.
   - If User Secrets are available and a bounded live smoke test is authorized, submit one query with at least three service pages, select **Fetch All Pages**, confirm request starts respect the configured one-second interval, observe progress reaching the actual loaded-record percentage and Data Page total, and verify the final aggregate. Do not log credentials or full sensitive records, and do not run an unbounded live crawl.
   - After implementation and verification, the coordinating agent must append one complete journal entry covering every changed implementation, test, configuration, and code-related documentation file to `C:\Source\Programs\Journal.md`. This pending plan is planning-only and is not part of that future implementation entry.

## 8. Acceptance criteria

- The results menu displays **Fetch All Pages** immediately below **Fetch Next Result Page** and before Save whenever another service data page is available.
- Selecting **Fetch All Pages** sequentially fetches every remaining cursor page for the current query, preserving page order and adding each successful response exactly once to the existing session aggregate.
- Every continuation request uses the unchanged dataset, query, operator, ordered fields, row limit, current cursor, and next service page number; no request is parallelized or issued directly by the pager.
- The existing authenticated iSearch pacing boundary is respected: continuation request starts are serialized and honor the configured minimum interval (1,000 ms in production), and bounded transient retries continue to honor `Retry-After`.
- The Search Summary updates after each accepted page with the new **Data Page X of Y**, loaded-record count, and a progress bar whose percentage equals the actual loaded records divided by the service `totalCount`, rendered to one invariant-culture decimal place for nonterminal values and capped at 100%.
- A partial final page does not display 100% until all `totalCount` records are loaded; an empty successful query displays 100% and offers no fetch action.
- The operation stops successfully when all records are loaded or the verified service terminal conditions confirm completion. Blank/unchanged cursors, early empty pages, contradictory totals, malformed responses, and retained-result capacity overflow cannot cause an infinite loop or a falsely complete walk.
- A failed continuation preserves all previously accepted pages, current-page state, and actual partial progress; the pager shows a safe message and permits retry or Back. Caller cancellation propagates with the existing cancellation semantics and closes the live summary cleanly.
- After successful completion, the pager shows the final service page, resets only the terminal display-page index, and keeps the selected database, return dataset, query, fields, and accumulated data intact.
- **Next Display Page** and **Previous Display Page** remain local presentation actions and never affect the all-pages aggregate or issue an iSearch request.
- Save continues to export the exact pages walked in the current session, including all pages loaded by **Fetch All Pages**, without fetching more data itself.
- Documentation accurately describes explicit all-pages behavior, actual progress, throttle/retry behavior, in-memory sensitivity, cancellation, partial failure, and the distinction between Data Page and Display Page.
- Focused tests, full build/test/format checks, whitespace verification, and documentation checks pass, with any live-service limitation reported accurately.

## 9. Risks and assumptions

- The current iSearch service contract supplies `cursor`, `returnedCount`, `totalCount`, and `results`; the plan assumes the existing parser's cardinality calculation remains authoritative for total pages, while the implementation must still guard against empty or unchanged continuation responses.
- A live result set could change between requests. The plan assumes a stable query snapshot and therefore treats a changed `totalCount` as contradictory rather than silently changing the progress denominator.
- A cursor may be present while cardinality indicates completion, or may disappear before `totalCount` is reached. The implementation must distinguish confirmed completion from incomplete termination and report the latter without discarding accepted data.
- Large result sets can consume substantial memory. The existing 1,048,575-result retention guard remains in force; a future streaming or bounded-export design is separate work.
- Live terminal rendering can interact poorly with noninteractive output or a terminal too short for the full summary. The implementation must use the repository's injectable `IAnsiConsole`, bounded layout, and deterministic tests, and must leave the ordinary static summary usable when live rendering is unavailable.
- The current tests use fake API clients and often disable timing delays. They must not be treated as proof of public-service pacing; pacing verification needs a dedicated controlled seam or a narrowly bounded timing test.
- The user requested all remaining pages for the current query, not a new query over every configured return dataset. Changing the return dataset remains outside this plan.

## 10. Deferred follow-up

- Streaming all pages directly to disk or a bounded consumer instead of retaining every record in memory.
- Resumable or checkpointed walks across CLI process visits.
- User-selected maximum page/record limits, estimated duration, pause/resume, or a cancel-and-save-partial-results workflow.
- Adaptive service-rate negotiation, cross-process throttle coordination, or service-specific crawl quotas beyond the existing client policy.
- Parallel dataset walks, query partitioning, deduplication, or stable-identifier reconciliation.
- Noninteractive command-line options or scheduled iSearch crawling.

## 11. Skill usage

- `plan-software-changes`: used to keep the work to one repository-grounded feature, define non-goals, identify ownership boundaries, and specify actionable verification.
- `isearch`: used for cursor continuation, `returnedCount`/`totalCount` progress, documented stop conditions, authenticated one-second pacing, `Retry-After`, bounded retries, and safe live-service testing.
- `spectre-console-cli`: used for the interactive `SelectionPrompt` lifecycle, cancellation propagation, injected `IAnsiConsole`, and version-pinned 0.55.0 live rendering considerations.
- `dotnet-automated-testing`: used to structure state-transition, boundary, cancellation, pacing, UI, and composition coverage with the repository's xUnit tests.
- `operation-result-pattern`: used to model expected partial-walk failures without hiding accepted pages or turning normal service outcomes into exception-driven control flow.
- `verbose-code-documentation` and `csharp-comment-spacing`: implementation guidance for XML contracts, implementation regions, intent-focused comments, and logical spacing on materially modified C# files.
- `dotnet-architectural-principles`: used to keep API protocol, session state, UI rendering, and composition responsibilities separated and to avoid a pager god class.
- `journal-entry-style`: confirms that the future implementation must journal one verified completion entry in the global `C:\Source\Programs\Journal.md`; this planning-only file does not trigger that coding completion gate.
