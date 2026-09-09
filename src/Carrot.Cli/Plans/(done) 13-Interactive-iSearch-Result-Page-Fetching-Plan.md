# Interactive iSearch Result-Page Fetching

**Status:** Complete

## 1. Outcome

Expand the interactive iSearch workflow so an operator can fetch the next service result page—the next bounded chunk of records represented by the current response cardinality—without changing the selected live database or configured return dataset.

The results view must distinguish two unrelated kinds of navigation:

- **Next Display Page** moves through terminal-sized lines already held for one service response and makes no network request.
- **Fetch Next Result Page** uses the current service cursor to request the next result chunk for the same database, query, return dataset, fields, and row limit.

The continuation mechanism will be a reusable, one-page-at-a-time session abstraction. The interactive pager will consume it now; future automated crawling can loop over the same bounded continuation contract, cancellation behavior, response validation, and cursor rules without reimplementing HTTP or page-state logic.

## 2. Problem

The current iSearch feature reports service cardinality and retains the response cursor, but it only submits the initial search request:

- `src/Carrot.Cli/ISearch/Contracts/SearchResponse.cs` exposes `Cursor`, `Results`, and typed `Cardinality`, but the cursor is currently reserved for future paging.
- `src/Carrot.Cli/ISearch/Contracts/SearchRequest.cs` represents the database, query, fields, operator, and row limit for the initial request; it has no explicit continuation operation.
- `src/Carrot.Cli/ISearch/ISearchApiClient.cs` builds one dataset-scoped search URI and derives the first result page as page `1` for nonempty data.
- `src/Carrot.Cli/Cli/UI/SearchResultsPager.cs` has a `Next` action, but it advances only the serialized JSON display-line page within the already fetched response. It never calls iSearch.
- `src/Carrot.Cli/Cli/UI/InteractiveISearchFlow.cs` submits a query and hands one `SearchResponse` to the pager. It does not retain a continuation-aware query session.

The existing label **Next Page** is therefore ambiguous in the context of a cardinality report: it can mean a terminal display page, while the requested capability means the next service result page. Without a shared continuation boundary, a future crawler would also need to duplicate cursor validation, request construction, page numbering, safe failures, rate limiting, and cancellation.

## 3. Solution vision

Keep the selected database and configured return dataset as immutable query context while adding a reusable page session around the current response.

1. The initial search continues to use `SearchRequest` and the existing authenticated GET pipeline. Its ordered `Fields`, `Rows`, query, database, and `defaultOp` become the stable context for all later pages.
2. Add one explicit continuation operation to the iSearch API boundary. It receives the stable `SearchRequest`, the current response cursor, and the expected next result-page number. A shared private search-request builder/parser handles both initial and continuation requests so query encoding, `fl`, authentication, response limits, retries, pacing, redirects, and diagnostics cannot drift.
3. Add a stateless-or-stepwise `SearchResultPageSession` abstraction that owns the current response and continuation rules. It exposes the current service page, whether a next result page is available, and one bounded `FetchNextAsync` transition. A successful transition replaces the current page; a failed transition leaves the current page available for retry or exit. The abstraction does not fetch in a loop, accumulate all records, persist data, or decide crawler limits.
4. Change the results pager to present two navigation layers with unambiguous names. Display-line navigation operates only on the current response. Result-page fetching invokes one session transition, resets the display-line index for the newly fetched response, and redraws the cardinality block with the new service page number.
5. Keep the selected live database and return dataset in `InteractiveISearchFlow`. Fetching a result page is not selecting the next configured return dataset, and it must not reopen or alter the return-dataset picker.

The API client owns service protocol and response invariants. The page session owns cursor/page-state transitions. The pager owns terminal rendering and local display navigation. The interactive flow owns query context and menu lifetime. A future crawler can consume the page session or the same API continuation contract as a bounded sequence without depending on Spectre.Console or duplicating the interactive flow.

Before implementation, confirm the exact iSearch request parameter and semantics for sending the response `cursor` back to the search endpoint from the service’s authoritative API contract or a controlled service response. The plan assumes the continuation is cursor-based, but the parameter name and whether the original query parameters must accompany it must be verified before the URI contract is finalized.

## 4. Scope

- Add an explicit one-page continuation operation to the iSearch contracts and HTTP client.
- Centralize initial and continuation request construction and response parsing.
- Add a reusable page-session/transition abstraction suitable for interactive use and future bounded crawling.
- Retain the current query context, cursor, typed cardinality, generic records, safe diagnostics, pacing, retries, timeout, redirect refusal, and cancellation behavior.
- Add distinct interactive labels and behavior for local display paging versus fetching the next service result page.
- Preserve the selected live database and configured return dataset while fetching result pages.
- Add focused API, session, pager, and interactive-flow tests plus documentation for the two navigation meanings and continuation behavior.
- Apply the repository’s C# documentation, implementation-region, comment-spacing, and global verification conventions to every materially modified C# file during implementation.

## 5. Non-goals

- Do not implement an unattended crawler, automatic full-dataset loop, scheduler, resume checkpoint, deduplication policy, or result export.
- Do not fetch the next configured return dataset or alter the **Select Return Dataset** workflow.
- Do not replace cursor continuation with offset pagination or invent a client-side cursor.
- Do not make terminal display pages represent service result pages; terminal height and JSON wrapping remain presentation concerns.
- Do not accumulate every fetched page in the interactive pager. The current page session should support one-step progression; a future crawler can choose its own bounded accumulation or streaming sink.
- Do not add query-field selection, sorting, filtering, authentication changes, live schema caching, or changes to the configured cardinality vocabulary.
- Do not make the initial request send cardinality names as `fl` fields.
- Do not alter unrelated pagers that use **Next Page** for their own local terminal views.

## 6. Technical approach

### Continuation contract

Keep `SearchRequest` as the stable query context and keep the cursor out of `Fields` and the configured return dataset. Add an explicit API operation, with final naming matched to repository conventions, such as:

```text
SearchAsync(SearchRequest request, CancellationToken cancellationToken)
SearchNextPageAsync(
    SearchRequest request,
    string cursor,
    int nextPageNumber,
    CancellationToken cancellationToken)
```

The continuation method must reject blank cursors, invalid page numbers, and invalid base requests before HTTP. It must reuse the same dataset path encoding, query encoding, ordered `fl`, row bound, `defaultOp`, credential gate, pacing, timeout, bounded response body, retry classification, redirect refusal, cancellation propagation, and safe error mapping as the initial request.

The private request builder should add the verified cursor query parameter only for continuation. The parser should accept the expected page number as context, preserve the returned cursor and generic records, validate count/record consistency, and calculate total result pages from the response total and request rows. For nonempty data, a continuation response reports the supplied one-based page number. Preserve the existing initial empty-result `0 of 0` semantics and define the service-valid empty continuation behavior in the implementation tests once the cursor contract is verified; no caller may invent a phantom result page.

### Reusable page session

Add a small `SearchResultPageSession` and interface/factory only if needed by the existing composition style, under `src/Carrot.Cli/ISearch/` or `src/Carrot.Cli/ISearch/Contracts/`. The session should contain:

- the stable `SearchRequest`;
- the latest successful `SearchResponse`;
- a `CanFetchNextPage` decision based on a nonblank service cursor and the cardinality/page termination rules;
- one `FetchNextAsync` method that calls the API continuation operation and updates state only after success.

The session is deliberately one-step and side-effect bounded. A future crawler can call `FetchNextAsync` in a loop with its own maximum-page, cancellation, delay, accumulation, and persistence policy. The interactive UI can call the same method once per operator action. No crawler policy should leak into the pager or API client.

### Two navigation layers

Refactor the private results actions in `SearchResultsPager` so the visible labels and dispatch make the boundary explicit:

| Layer | Suggested visible action | Effect | Network request |
| --- | --- | --- | --- |
| Terminal display | `Next Display Page` | Advances the line slice of the current response | No |
| Terminal display | `Previous Display Page` | Moves back within the current response’s rendered lines | No |
| Service data | `Fetch Next Result Page` | Uses the cursor to retrieve the next bounded record chunk | One, when available |
| Workflow | `Back to iSearch` | Leaves the result view with the selected context retained | No |

Do not use a bare **Next Page** label for the iSearch results view after this change. The pager title and cardinality panel should identify both `Result Page X of Y` and `Display Page X of Y`. When a result page is fetched, reset only the display-page index; the selected database, return dataset, query, fields, row limit, and session remain unchanged. If fetching fails, render the safe operation messages while retaining the current response and offering the same retry/back choices.

The current response is the display source. This avoids mixing records from different service pages in one terminal page and keeps the visual page count independent from cardinality. The session remains the source of truth for whether another service page can be fetched.

### Alternatives considered

- **Rename the existing `Next` action and add cursor fetching directly to `SearchResultsPager`:** rejected because it would make terminal presentation own HTTP continuation and would not give future crawling a reusable boundary.
- **Add an optional cursor to `SearchRequest` and make callers mutate it:** rejected because a service continuation token is not part of the stable query definition, and mutable request reuse makes page transitions easy to duplicate or skip.
- **Fetch all cursor pages inside `SearchAsync`:** rejected because it would turn one bounded request into an implicit network loop, remove operator control, complicate cancellation and partial-failure handling, and preempt the future crawler’s accumulation policy.
- **Use total pages alone to offer the next action:** rejected because the service cursor is the authoritative continuation capability; cardinality describes counts, while a missing cursor must disable fetching even when counts suggest more data.
- **Use offset/page-number requests instead of the cursor:** rejected because the current response already exposes a service-owned cursor and offset semantics are not established by the repository.

## 7. Implementation steps

1. **Verify and model the iSearch continuation contract.**
   - Inspect the authoritative iSearch API contract available to the implementation and confirm the continuation query parameter, required accompanying parameters, cursor encoding, terminal cursor behavior, and response shape.
   - Update `src/Carrot.Cli/ISearch/Contracts/SearchRequest.cs` only if a stable query-context adjustment is required; do not place cursor values in `Fields` or mutate configured return definitions.
   - Update `src/Carrot.Cli/ISearch/Contracts/SearchResponse.cs` and `src/Carrot.Cli/ISearch/Contracts/SearchCardinality.cs` documentation to distinguish service result pages, terminal display pages, current cursors, and empty/terminal responses.
   - If the verified contract needs a dedicated continuation value object, add it under `src/Carrot.Cli/ISearch/Contracts/` with explicit validation semantics rather than passing an unstructured collection of strings and integers.
   - Verify the model remains generic for all configured return datasets and does not introduce dataset-specific record DTOs.

2. **Centralize initial and continuation HTTP behavior.**
   - Update `src/Carrot.Cli/ISearch/IISearchApiClient.cs` with the explicit continuation operation and XML contract, including preconditions, one-page behavior, cursor semantics, cancellation, and safe failures.
   - Refactor `src/Carrot.Cli/ISearch/ISearchApiClient.cs` so initial and continuation searches share one URI builder and one bounded send/parse pipeline. The only protocol difference should be the verified cursor parameter and expected page number.
   - Preserve the current authenticated GET route, `fl` ordering, row maximum, retry/pacing gate, timeout, response-size limit, redirect refusal, and credential-safe diagnostics.
   - Pass page context into the common parser so the initial response remains page `1` when nonempty and later responses report the requested next page without recalculating presentation pages.
   - Reject malformed continuation responses, contradictory counts, missing required arrays, invalid cursor types, and invalid page transitions through the existing operation-result failure convention. Keep the last successful page usable after a failed fetch.
   - Extend `tests/Carrot.Cli.Tests/ISearch/ISearchApiClientTests.cs` for the verified continuation URI, cursor encoding, preservation of query/database/fields/rows, next-page cardinality, cursor replacement, no-cursor/invalid-page rejection, malformed responses, empty/terminal behavior, cancellation, retries, redirects, and safe bounded diagnostics. Assert that cardinality names never enter `fl`.

3. **Build the reusable page-session boundary.**
   - Add `src/Carrot.Cli/ISearch/SearchResultPageSession.cs` and, if required by the dependency-injection/test style, `ISearchResultPageSession.cs` or a focused factory. Keep the abstraction independent of Spectre.Console and terminal dimensions.
   - Initialize it from the original `SearchRequest`, the first successful `SearchResponse`, and `IISearchApiClient`; expose the current page and an explicit `CanFetchNextPage` result.
   - Implement one-step `FetchNextAsync` using the current cursor and cardinality page number. Update the current page only after a successful operation; never lose the prior page on a failed request or cancellation.
   - Ensure the session is usable by a future bounded crawler without forcing an in-memory all-results collection. Document that the caller owns looping, maximum-page policy, aggregation/streaming, persistence, and checkpointing.
   - Add `tests/Carrot.Cli.Tests/ISearch/SearchResultPageSessionTests.cs` or extend the closest existing iSearch test file for initial state, available/unavailable continuation, page-number advancement, successful replacement, failed-fetch retention, repeated calls, cancellation, and terminal cursor behavior.

4. **Integrate result-page fetching without conflating display paging.**
   - Update `src/Carrot.Cli/Cli/UI/InteractiveISearchFlow.cs` to retain the original `SearchRequest` alongside the initial response and create/pass the page session to the results pager. Keep the selected database and `SearchReturnTypeDefinition` unchanged for the entire result visit.
   - Update `src/Carrot.Cli/Cli/UI/ISearchResultsPager.cs` and `src/Carrot.Cli/Cli/UI/SearchResultsPager.cs` to consume the session or an equivalent continuation abstraction, while preserving the current generic JSON rendering and literal terminal safety.
   - Split the existing local `PageChoice.Next`/`Previous` terminology into explicit display actions and add a separate `Fetch Next Result Page` action only when the session reports availability. Do not issue an API request for display navigation.
   - On a successful remote fetch, reset the local display-line index, render the new service cardinality, and keep the pager open. On failure, show safe messages and leave the existing records/cardinality displayed or available for retry. On Escape/Back, return to the dataset menu without clearing the selected database or return dataset.
   - Update `src/Carrot.Cli/Composition/ServiceRegistration.cs` only if the session/factory requires registration; avoid registering UI concerns in the API client.
   - Extend `tests/Carrot.Cli.Tests/ISearch/SearchResultsPagerTests.cs` and `tests/Carrot.Cli.Tests/ISearch/InteractiveISearchFlowTests.cs` to prove: display navigation makes zero additional calls; fetching makes exactly one call with the same query context and cursor; the next response replaces the displayed chunk; the title distinguishes result and display pages; the action is absent without a cursor or at the terminal page; failed fetches retain the current page; and changing/fetching pages never invokes or changes the return-dataset selection.

5. **Document the two meanings of page and the future crawling boundary.**
   - Update `src/Carrot.Cli/Docs/isearch.md` with the distinct **Next Display Page** and **Fetch Next Result Page** actions, the cursor-based request flow, the unchanged selected database/return dataset, page/cardinality examples, terminal behavior, failure/retry behavior, and the fact that the current interactive workflow fetches one page only when requested.
   - Update `docs/cli-reference.md`, `src/Carrot.Cli/Docs/getting-started.md`, and `README.md` only where their iSearch summaries currently describe cursor retention or result paging. Make the distinction explicit and do not claim that an automated crawler exists.
   - Keep the embedded Markdown help workflow unchanged; update any help-resource tests that assert exact iSearch wording or topic content.
   - Describe the reusable page session as the future crawler’s foundation, while leaving crawler scheduling, persistence, and aggregation to a later plan.

6. **Verify the complete feature.**
   - Run focused API-client, page-session, results-pager, and interactive-flow tests first.
   - Run `dotnet build .\Carrot-CLI.slnx --no-restore`, `dotnet test .\Carrot-CLI.slnx --no-build --no-restore`, `dotnet format .\Carrot-CLI.slnx --verify-no-changes --no-restore`, and `git diff --check`.
   - Confirm documentation-convention tests pass for all touched C# types and members, including any new session contract, and confirm the project’s XML documentation output remains enabled.
   - With configured credentials and an authorized bounded smoke test, submit one initial query, choose the explicit fetch action once, verify the second request carries the verified cursor and the same query context, and confirm the result-page cardinality advances while display-page navigation remains local. Do not print credentials, cookies, or full sensitive result payloads.
   - Do not run an unbounded live walk. If the service contract or environment cannot verify continuation safely, record that limitation and leave the code gated behind the focused deterministic tests until the contract is resolved.
   - After implementation and verification, the coordinating agent must append one complete journal entry for every changed implementation, test, configuration, and code-related documentation file to `C:\Source\Programs\Journal.md`. This pending plan is planning-only and is not part of that future implementation entry.

## 8. Acceptance criteria

- The initial iSearch query behavior remains unchanged and returns a session with its original database, query, configured return fields, row limit, response records, cursor, and cardinality.
- The results view visibly distinguishes **Next Display Page** from **Fetch Next Result Page**; no bare ambiguous **Next Page** action remains in the iSearch results pager.
- Selecting **Next Display Page** changes only terminal display-line navigation and performs no HTTP request or service-page transition.
- Selecting **Fetch Next Result Page** performs exactly one bounded authenticated continuation request using the current service cursor and the same selected database, query, operator, ordered fields, and row limit.
- A successful continuation replaces the current visible record chunk, advances the service result-page cardinality, preserves the new cursor, resets only the terminal display-page index, and leaves the selected return dataset unchanged.
- The fetch action is unavailable when there is no usable cursor or the service/cardinality rules indicate that no next result page exists. A terminal or empty response follows the verified service contract without inventing a phantom page.
- A failed continuation or cancellation does not discard the last successful page, does not silently advance page state, and does not expose credentials or unbounded response content.
- The reusable page-session boundary supports one-step advancement and can be looped by a future crawler without depending on terminal UI, while this feature itself performs no automatic full-dataset crawl.
- Generic records, cardinality field-name configuration, `fl` contents, authentication, pacing, retry, timeout, response bounds, redirect handling, and cancellation remain compatible with the existing iSearch feature.
- Focused and full tests cover request construction, cursor/page invariants, two navigation layers, selected-dataset retention, empty/terminal behavior, failures, cancellation, and future-session usability; build, test, format, whitespace, and documentation checks pass.

## 9. Risks and assumptions

- The repository currently documents the response cursor but not the complete continuation request contract. The exact query parameter, required request context, and terminal cursor behavior must be verified before implementation; do not infer them solely from the response property name.
- iSearch may return a cursor even when the computed cardinality suggests the final page, or omit a cursor before the computed total is exhausted. The cursor and cardinality termination rules must be tested together, with the service contract treated as authoritative and contradictory responses rejected or safely terminated.
- A result response may contain records whose serialized representation spans multiple terminal lines. Display-page counts will therefore never be used to calculate or control service-page fetching.
- A user may retry a failed continuation. Keeping the prior successful response until a new response is validated is required to avoid losing data or advancing a crawler state incorrectly.
- Future automated crawling may need accumulation, streaming, checkpointing, rate budgets, and restart semantics. The one-step session should expose enough stable continuation state for that work but must not decide those policies prematurely.
- The plan assumes fetching the next service page should keep the current database, query, configured return dataset, and fields fixed. If the intended meaning is instead to cycle through configured return datasets, that is a separate feature and requires a different plan.

## 10. Deferred follow-up

- Bounded automated crawling over the reusable page session, including maximum-page controls, aggregation or streaming sinks, persistence, checkpoints, resume, deduplication, and restart behavior.
- Noninteractive iSearch commands or scheduled data-crawl jobs.
- Result-page caching and explicit previous-service-page navigation beyond any page state deliberately retained by a future crawler.
- Adaptive row limits, backoff policies specific to crawling, parallel page fetching, rate-budget coordination across datasets, and service-specific checkpoint recovery.
- Query partitioning or dataset sharding for result sets beyond the service’s supported cursor/cardinality range.
