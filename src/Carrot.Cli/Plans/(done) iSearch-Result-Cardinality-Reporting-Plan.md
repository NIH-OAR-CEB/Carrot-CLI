# iSearch Result Cardinality Reporting

**Status:** Complete

## 1. Outcome

Make every configured iSearch return type report a common, typed cardinality block alongside its returned records. The block will expose the total matching result count, the number returned by the current iSearch response, the current result-page number, and the total number of result pages.

Reshape `iSearchReturnTypes` so shared cardinality field names live under `Results` above return types such as `Grants`:

```json
{
  "iSearchReturnTypes": {
    "Results": {
      "Cardinality": {
        "TotalResultsFieldName": "totalCount",
        "CurrentResultsFieldName": "returnedCount",
        "PageNumberFieldName": "pageNumber",
        "TotalPagesFieldName": "totalPages"
      },
      "Grants": {
        "DefaultFields": [
          "nihApplId",
          "applTypeCode",
          "fundingCategory",
          "activityCode",
          "grantNumber",
          "fullGrantNumber",
          "calculatedTotalCostAmount",
          "fy",
          "grantOrganization",
          "title",
          "abstract",
          "specificAims",
          "zuliaScore"
        ]
      }
    }
  }
}
```

The configured cardinality names are report/property names and do not become iSearch `fl` fields. The service-owned `totalCount`, `returnedCount`, `cursor`, and `results` envelope remains authoritative; nonempty responses start at `pageNumber` `1`, and `totalPages` is calculated from `totalCount` and the requested `rows` value. A zero-result response reports `pageNumber` `0` and `totalPages` `0`, so a data walk can terminate without inventing an empty page. The existing cursor and these counts remain available for user-controlled paging and future bounded automation.

## 2. Problem

The current implementation has the raw count values but not a reusable common cardinality model:

- `src/Carrot.Cli/appsettings.json` places `Grants` directly under `iSearchReturnTypes` and contains only its `DefaultFields`.
- `src/Carrot.Cli/Configuration/SearchReturnTypeCatalog.cs` treats every child of `iSearchReturnTypes` as a return type, so it has no place to bind shared cardinality names.
- `src/Carrot.Cli/ISearch/Contracts/SearchResponse.cs` exposes `ReturnedCount` and `TotalCount` as separate envelope properties, plus an optional `Cursor`, but has no nested page/cardinality object.
- `src/Carrot.Cli/Cli/UI/SearchResultsPager.cs` displays `returned X of Y total` and computes a terminal display-line page count. That page count is not the iSearch result-page count needed to understand dataset traversal.
- `src/Carrot.Cli/ISearch/ISearchApiClient.cs` validates the response envelope but does not calculate or expose page number and total pages as one reportable contract.

As a result, `Grants` and future return types cannot share one explicit cardinality reporting contract, and the operator cannot distinguish terminal display pages from pages of the bounded iSearch dataset response.

## 3. Solution vision

Keep configuration, service response parsing, and presentation responsibilities separate:

1. Bind `iSearchReturnTypes.Results` into a container with one shared `Cardinality` definition and ordered child return types. `SearchReturnTypeCatalog` will retain the existing field-order behavior for `Grants`, while recognizing `Cardinality` as metadata rather than a selectable return type.
2. Add a nested `SearchCardinality` model to `SearchResponse`. It will hold typed values for total results, results in the current response, current result page, and total result pages. The response will continue to retain the generic JSON records and cursor because iSearch datasets vary at runtime.
3. Calculate the first response’s server-page metadata at the iSearch boundary from the validated response and the request’s bounded row count. The calculation will use ceiling division for nonempty data and return zero pages for an empty result set. The `cursor` remains the continuation token required by iSearch’s documented cursor-based paging contract, so a caller can offer user paging now and add bounded automated page walking later.
4. Pass the shared cardinality field-name configuration to `SearchResultsPager`, which will render the configured labels and typed values consistently for `Grants` and every other configured return type. Its terminal line paging remains separate and will continue to govern how serialized JSON fits on screen.

The configuration catalog owns shape and name validation. `ISearchApiClient` owns iSearch envelope validation and cardinality calculation. `SearchResponse` owns the transport-independent nested contract. `SearchResultsPager` owns literal terminal reporting. `InteractiveISearchFlow` continues to select a return type and hand the response to the pager; it should not parse configuration sections or calculate counts.

This follows the existing SOLID boundaries: the common model removes duplicated reporting knowledge without making dataset-specific record models, while the API client remains the only component that interprets the iSearch response envelope. No new HTTP client, persistence layer, or automatic export pipeline is needed.

## 4. Scope

- Replace the checked-in `iSearchReturnTypes` shape with the `Results.Cardinality` plus nested return-type shape.
- Add a validated configuration model for the four shared cardinality field names.
- Update return-type catalog loading and DI usage for the new container shape.
- Add a nested typed cardinality model to the iSearch search response.
- Calculate and preserve current page and total page values for each response.
- Render the configured cardinality labels and values for all configured return types, including `Grants`.
- Add focused configuration, response parsing, and pager/interactive-flow tests.
- Update iSearch help, getting-started text, the CLI reference, and README wording/examples.
- Fully document every new or materially modified C# type, method, and property with the required XML header, remarks, references, and implementation regions; add intent-focused internal comments and logical vertical spacing in changed C# paths.

## 5. Non-goals

- Do not add automatic fetching of every cursor page or an unbounded dataset walk.
- Do not replace iSearch cursor navigation with offset pagination; the cursor remains the service-owned continuation mechanism.
- Do not add cardinality names to the `DefaultFields` arrays or send them as `fl` result fields.
- Do not change dataset discovery, query syntax, authentication, request limits, retry/pacing behavior, or generic record serialization.
- Do not treat the terminal JSON display-line count as the service result-page count.
- Do not introduce per-dataset record DTOs, result persistence, exports, caching, or named noninteractive iSearch commands.
- Do not support both the old flat configuration and the new nested configuration unless implementation reveals an established compatibility requirement; the checked-in configuration is the migration source of truth.

## 6. Technical approach

### Configuration contract

Use a dedicated configuration model such as `SearchResultConfiguration` containing:

- one `SearchCardinalityFieldNames` value for `Cardinality`;
- an ordered read-only list of `SearchReturnTypeDefinition` values for siblings such as `Grants`.

The four configuration properties should be explicit and stable: `TotalResultsFieldName`, `CurrentResultsFieldName`, `PageNumberFieldName`, and `TotalPagesFieldName`. Their values are the field/property names used when the common cardinality block is rendered. Require all four names to be nonblank and distinct. Reject unknown objects in the `Results` container rather than silently exposing a malformed selectable return type. Preserve child and `DefaultFields` order.

The current `Grants.DefaultFields` values remain unchanged under `Results`. Existing field validation still rejects missing fields, blank values, and duplicates. Invalid return configuration should remain feature-local: entering iSearch reports the safe diagnostic and makes no search request, while unrelated CLI startup remains available.

### Response and page calculation

Keep the documented iSearch envelope fields at the HTTP boundary. Validate that `returnedCount`, `totalCount`, and `results` exist, are integer/array values of the expected shape, are nonnegative, and that `returnedCount` equals the number of returned records. Preserve `cursor` when it is a string.

Add a nested model, for example `SearchCardinality`, to `SearchResponse`:

```text
Cardinality
  TotalResults
  CurrentResults
  PageNumber
  TotalPages
```

For a nonempty initial bounded request, set `CurrentResults` from `returnedCount`, `TotalResults` from `totalCount`, `PageNumber` to `1`, and `TotalPages` to `ceiling(totalCount / rows)`. For an empty response, set `CurrentResults` and `TotalResults` to `0`, and set both `PageNumber` and `TotalPages` to `0`; this gives a caller a clear terminal condition for a data walk. Keep the requested `rows` available to the calculation by passing the request context into the parser or applying the derived metadata immediately after parsing. If a future cursor-page request is added, its caller can supply the current page number without changing the nested report contract.

The configuration names should label the report, not override the service envelope property names. This avoids pretending that `pageNumber` and `totalPages` are fields supplied by the current iSearch API while still giving operators an explicit stable vocabulary for output.

### Presentation

Update `ISearchResultsPager`/`SearchResultsPager` contracts only as needed to receive the shared field-name model or a response that already carries it. Render one literal cardinality summary before the records, for example:

```text
Total Results: 201 | Current Results: 100 | Result Page: 1 of 3
```

Use the configured names when rendering labels, escape any terminal markup, and retain the current bounded JSON-line paging. The pager title may continue to say `Page N of M` for terminal display pages, but the cardinality summary must identify its values as result pages so the two concepts are not confused.

### Alternatives considered

- **Keep four top-level properties on `SearchResponse`:** rejected because it duplicates common cardinality semantics across consumers and does not provide the requested nested reporting model.
- **Put `Cardinality` inside each `Grants`/return-type object:** rejected because the field names and meaning are common to every return type; duplicating them invites drift.
- **Add `totalCount`, `returnedCount`, and page values to `DefaultFields`:** rejected because iSearch treats `fl` as record-field selection while counts and cursor are response-envelope metadata.
- **Use terminal display pages as result pages:** rejected because serialized records can span multiple terminal lines and that count changes with terminal dimensions.
- **Fetch all cursor pages now:** rejected because it expands a reporting change into an unbounded network traversal and conflicts with the iSearch skill’s bounded-walk guidance.

## 7. Implementation steps

1. **Reshape and validate iSearch return configuration.**
   - Modify `src/Carrot.Cli/appsettings.json` so `iSearchReturnTypes.Results.Cardinality` appears before the `Grants` child and contains the four explicit names shown in the outcome.
   - Add the configuration model(s) under `src/Carrot.Cli/Configuration/`, documenting the shared contract, field-name constraints, ordering, and relationship to `SearchReturnTypeDefinition`.
   - Update `src/Carrot.Cli/Configuration/SearchReturnTypeCatalog.cs` to bind `Results`, validate `Cardinality`, ignore its metadata role when building selectable definitions, and continue validating all return-type `DefaultFields`.
   - Update `src/Carrot.Cli/Composition/ServiceRegistration.cs` only if the catalog registration or a pager dependency must change; preserve lazy feature validation and optional iSearch startup.
   - Extend `tests/Carrot.Cli.Tests/Configuration/SearchReturnTypeCatalogTests.cs` for the new shape, exact name/order preservation, missing/blank/duplicate cardinality names, missing `Results`, missing return groups, and invalid configuration isolation.

2. **Introduce the nested response cardinality contract.**
   - Add a documented model under `src/Carrot.Cli/ISearch/Contracts/` (or a documented nested type beside `SearchResponse`) for `SearchCardinality` with the four typed values and clear semantics for an empty result set.
   - Modify `src/Carrot.Cli/ISearch/Contracts/SearchResponse.cs` to expose the nested cardinality object while retaining the cursor and generic records. Update XML documentation to distinguish service response counts from terminal display paging.
   - Modify `src/Carrot.Cli/ISearch/ISearchApiClient.cs` so `SearchAsync` supplies the request row context to parsing/derivation, validates nonnegative counts and count/record consistency, calculates `PageNumber` and `TotalPages`, and preserves the existing safe error and cancellation behavior.
   - Update `src/Carrot.Cli/ISearch/IISearchApiClient.cs` documentation to describe the nested cardinality contract and cursor semantics.
   - Extend `tests/Carrot.Cli.Tests/ISearch/ISearchApiClientTests.cs` with first-page calculations for full, partial, and empty responses; malformed/negative/inconsistent count cases; cursor preservation; and a request with a non-default row count proving ceiling division. Assert that empty data is `0` current results, `0` total results, page `0` of `0`. Keep synthetic credentials and ensure no credential appears in diagnostics.

3. **Report common cardinality for every return type.**
   - Modify `src/Carrot.Cli/Cli/UI/SearchResultsPager.cs` and its interface if needed so the pager receives the shared cardinality field names and renders total results, current results, result page, and total result pages before records.
   - Keep terminal line paging independently bounded and test that changing terminal height changes only display-page navigation, not the cardinality values.
   - Modify `src/Carrot.Cli/Cli/UI/InteractiveISearchFlow.cs` only where necessary to pass the common configuration through the existing selected-return-type path. Do not move count calculation into the flow or duplicate the four fields per return type.
   - Add `tests/Carrot.Cli.Tests/ISearch/SearchResultsPagerTests.cs` if no focused pager test exists; otherwise extend the existing iSearch flow tests. Cover `Grants`, a second synthetic return type, configured labels, empty results, multi-page result totals, and Escape/Back behavior.

4. **Update operator and developer-facing documentation.**
   - Update `src/Carrot.Cli/Docs/isearch.md` with the nested configuration shape, meaning of the four `*FieldName` settings, sample output, zero-result `0 of 0` semantics, distinction between result pages and terminal display pages, cursor-based user continuation, and the bounded/non-automatic automation boundary.
   - Update `src/Carrot.Cli/Docs/getting-started.md`, `docs/cli-reference.md`, and `README.md` only where their iSearch summaries need to describe the common cardinality report and new nested configuration.
   - Keep the embedded Markdown resource workflow intact and avoid secrets, live identifiers not already documented, or claims that the current flow automatically walks all results.
   - Update any affected C# XML comments and internal comments in the same implementation pass. Follow `verbose-code-documentation` and `csharp-comment-spacing`: every touched type/member gets the required divider and XML contract, non-obvious page calculations get intent comments, and changed bodies retain `#region implementation` with logical blank-line spacing.

5. **Verify the complete feature.**
   - Run focused configuration, iSearch API, pager, and interactive-flow tests first.
   - Run `dotnet build .\Carrot-CLI.slnx --no-restore`, `dotnet test .\Carrot-CLI.slnx --no-build --no-restore`, `dotnet format .\Carrot-CLI.slnx --verify-no-changes --no-restore`, and `git diff --check`.
   - Confirm `src/Carrot.Cli/Carrot.Cli.csproj` continues to emit XML documentation and that the documentation-convention tests pass for all touched C# files.
   - Exercise the help renderer or its existing tests to confirm the nested iSearch example and cardinality explanation are embedded and readable.
   - Do not call the live iSearch API unless the required User Secrets `iSearch:apiKey` and `iSearch:contactEmail` are present. If a live smoke test is authorized and available, use one bounded request, verify the documented envelope/count behavior, and do not print credentials.
   - After implementation—not while this plan is pending—the coordinating agent must append one verified entry covering every changed code, test, configuration, and code-related documentation file to `C:\Source\Programs\Journal.md`.

## 8. Acceptance criteria

- The checked-in configuration has `iSearchReturnTypes.Results.Cardinality` above `Grants`, and `Grants.DefaultFields` remains ordered and unchanged in content.
- The catalog exposes one validated shared cardinality definition and all configured return types; `Cardinality` is not presented as a selectable return dataset.
- Missing, blank, duplicate, or malformed cardinality names fail safely before an iSearch query, without breaking unrelated CLI startup.
- Every successful `SearchResponse` contains a nested cardinality model with total results, current returned results, page number, and total pages; its cursor and generic records remain available.
- For a nonempty initial request, page number is `1` and total pages uses ceiling division by the request row limit; an empty dataset reports zero current results, page `0`, and `0` total pages so a data walk has an unambiguous stop condition.
- The response retains the iSearch cursor together with the cardinality block, providing enough information for user-controlled next-page requests and future bounded automated walks.
- The terminal report uses the configured labels and clearly distinguishes result-page cardinality from terminal display-line pages for `Grants` and at least one additional return type.
- The `fl` query contains only the selected return type’s `DefaultFields`; cardinality names are not sent as result fields.
- Existing iSearch authentication, pacing, bounded rows, retry, timeout, redirect, cancellation, and safe diagnostic behavior remains unchanged.
- Focused and full .NET tests cover configuration validation, nested response parsing, count boundaries, page calculations, cursor retention, multiple return types, empty results, display paging, and documentation resources.
- Build, test, format, whitespace, and documentation checks pass, with any live-service verification limitation explicitly reported.

## 9. Risks and assumptions

- The requested explicit names are interpreted as `TotalResultsFieldName`, `CurrentResultsFieldName`, `PageNumberFieldName`, and `TotalPagesFieldName`, with service-property values `totalCount`, `returnedCount`, `pageNumber`, and `totalPages`. If the desired JSON key/value vocabulary differs, update the contract before implementation.
- The current iSearch API documentation guarantees `cursor`, `returnedCount`, `totalCount`, and `results`, but does not guarantee server-supplied page-number fields. This plan derives page metadata locally and does not claim that `pageNumber` or `totalPages` are returned by iSearch.
- Because the current UI submits only an initial bounded search, nonempty responses will report page `1` for this phase and empty responses page `0` of `0`. Cursor-aware next-page requests and incrementing page numbers remain a future extension of the same nested model; the current response already exposes the cursor and counts needed to build that extension safely.
- Changing the configuration from flat `iSearchReturnTypes.Grants` to nested `iSearchReturnTypes.Results.Grants` is a configuration compatibility change. The plan assumes checked-in configuration and operator documentation can migrate together; deployment-specific overrides may need the same reshape.
- `TotalPages` is based on the request’s `rows` value, not terminal height. A future caller that requests a different page size must use that request size for the calculation.
- No live API call is required to create this plan. Any implementation smoke test must follow the iSearch skill’s credential, rate-limit, and secret-handling rules.

## 10. Deferred follow-up

- Add an explicit cursor/page request contract and bounded **Next Result Page** network action that repeats the original query and fields with the returned cursor.
- Accumulate or export multiple server pages under an explicit operator-selected page/result limit.
- Validate configured `DefaultFields` against live `/fields/{dataset}` metadata before submitting a query.
- Add per-return-type cardinality formatting, localized labels, or persisted paging state if a separate product requirement emerges.

## 11. Skill usage

- `plan-software-changes`: used to ground the plan in repository symbols, define one coherent implementation phase, state non-goals, and specify verification.
- `isearch`: used for the authoritative response-envelope and cursor-pagination contract, the `fl`/`rows` semantics, bounded traversal guidance, and credential-safe verification rules.
- `dotnet-architectural-principles`: used to keep configuration parsing, API-boundary mapping, response modeling, and terminal reporting as separate responsibilities.
- `dotnet-automated-testing`: used to define focused xUnit tests, boundary cases, isolated HTTP handlers, and a small interactive/presentation integration layer.
- `verbose-code-documentation`: used to require complete XML documentation, related references, implementation regions, and documentation-pipeline verification for all touched C# members.
- `csharp-comment-spacing`: used to require intent-focused comments and logical vertical spacing for page calculations, response validation, and state transitions.

The Python documentation skill is not applicable because this change targets a .NET/C# CLI and its Markdown help resources.
