# Interactive iSearch Return Field-Set Selection

**Status:** Complete

## 1. Outcome

Allow an operator to choose a configured return field set before submitting an interactive iSearch query. After a live database is selected, the iSearch submenu will show **Select Return Dataset** immediately beneath **Select Database**. The choices will come from the `iSearchReturnTypes` configuration section, and the selected field set will be sent to iSearch as the request's `fl` result-field list.

The existing `Grants.DefaultFields` configuration will remain the first return-group definition. Additional return groups can be added as sibling entries without changing code.

## 2. Problem

The repository already has the configuration seed but does not consume it:

- `src/Carrot.Cli/appsettings.json` defines `iSearchReturnTypes.Grants.DefaultFields`, but no configuration model or catalog reads that section.
- `src/Carrot.Cli/Cli/UI/InteractiveISearchFlow.cs` retains only `selectedDatabase`; its menu currently offers **Select Database**, **View Fields**, **Submit Query**, and **Back to Main Menu**. There is no return-group selection or query prerequisite.
- `src/Carrot.Cli/ISearch/Contracts/SearchRequest.cs` contains dataset, query, operator, and row-limit values, but no result-field collection.
- `src/Carrot.Cli/ISearch/ISearchApiClient.cs` builds `GET /search/{dataset}` with `q`, `defaultOp`, and `rows`, but never sends iSearch's `fl` result-field parameter.

As a result, users cannot control which fields are returned and the configured `DefaultFields` list has no observable effect.

## 3. Solution vision

Introduce a small, strongly typed return-group catalog at the configuration boundary. It will bind the sibling keys beneath `iSearchReturnTypes` into operator-facing definitions containing the group name and its ordered `DefaultFields`. The catalog validates the configuration when the iSearch workflow is entered, while keeping iSearch optional for all other CLI commands.

Extend the current dataset-menu state with a selected return group. The flow will remain:

1. Validate iSearch credentials, check health, and discover live databases as today.
2. Let the operator select a live database.
3. Offer **Select Return Dataset** directly below **Select Database** and populate it only from the configured return-group catalog.
4. Enable **Submit Query** only after both a database and a return group have been selected.
5. Submit the selected group's fields in the existing bounded GET search and retain both selections when returning from results or field discovery.

The configuration catalog owns group names, field ordering, and local validation. `InteractiveISearchFlow` owns menu state and prompts. `SearchRequest` carries the selected fields across the UI/API boundary. `ISearchApiClient` owns iSearch URI encoding and serializes the fields as `fl`; it remains independent of Spectre.Console and configuration-section traversal. This preserves the existing separation between interactive orchestration and authenticated HTTP.

The menu label will say **Select Return Dataset** to match the requested operator terminology, while documentation will explain that its choices are configured return groups and that each group represents a field set, not another live `/datasets` discovery call.

## 4. Scope

- Bind and validate the existing `iSearchReturnTypes` sibling-key configuration shape.
- Add a return-group definition/catalog abstraction and register it through the existing composition root.
- Add return-field selection state and the **Select Return Dataset** menu action beneath database selection.
- Add the selected fields to `SearchRequest` and serialize them as the iSearch `fl` parameter in the dataset-scoped GET search.
- Require a valid return-group selection before an interactive query can run; clear a prior return-group selection when the database changes if the implementation cannot prove the field set is still appropriate.
- Add focused configuration, API-client, and interactive-flow tests.
- Update the existing iSearch help/reference documentation and examples without adding a new help topic or exposing credentials.

## 5. Non-goals

- Do not change iSearch authentication, health checks, live database discovery, query text semantics, row limit, result paging, or field metadata discovery.
- Do not make `iSearchReturnTypes` a second source of live database names or silently substitute a configured group for the selected live database.
- Do not automatically fetch `/fields/{dataset}` to rewrite, intersect, or reorder a configured field set; iSearch remains authoritative for whether the selected fields are valid for the selected database, and its safe error response is displayed.
- Do not add arbitrary per-query field editing, field autocomplete, filters, sort selection, exports, caching, persistence, or named noninteractive iSearch commands.
- Do not introduce a new HTTP client, a new authentication path, a new UI pager, or a repository-local secret file.

## 6. Technical approach

### Configuration and validation

- Add a focused configuration model/catalog under `src/Carrot.Cli/Configuration/` for the section whose children are named groups such as `Grants`, each containing `DefaultFields`.
- Preserve the current JSON shape and field order. Treat the configuration child key as the visible return-group label and each `DefaultFields` item as an exact iSearch field name.
- Validate before health/dataset requests are made: the section must contain at least one group; group labels must be nonblank; every group must contain at least one nonblank field; and duplicate field names within one group should be rejected rather than sent ambiguously. Preserve field order and exact spelling after validation.
- Keep credentials and return-group validation feature-local. Missing or invalid return-group configuration should produce a safe iSearch message and no query request, without preventing other CLI commands from starting.
- Register the catalog through `ServiceRegistration` using the existing Generic Host configuration. Avoid binding directly inside the UI so the catalog can be unit-tested independently.

### Request and iSearch boundary

- Add an ordered `Fields`/result-fields property to `SearchRequest`, documented as the configured fields for the selected return group.
- Extend `ISearchApiClient.SearchAsync` validation so an interactive search cannot be created without a nonempty, valid field list. Keep the existing dataset/query/`AND`/1-100 row constraints.
- Append `fl` to `GET /search/{dataset}` using iSearch's comma-separated array convention (`fieldA,fieldB`) and URI-encode the complete value. Preserve the configured order; do not place the API key or other secrets in the URI.
- Retain the existing credential gate, timeout, cancellation, redirect refusal, pacing, retry, bounded diagnostics, generic JSON result envelope, and safe error handling. The only HTTP contract change is the additional result-field query parameter.

### Interactive menu behavior

- Extend the private dataset-menu choice model and state in `InteractiveISearchFlow` with a selected return group.
- When no database is selected, show **Select Database** and **Back to Main Menu** only. Once a database is selected, show **Select Database**, **Select Return Dataset**, then the existing **View Fields** action; show **Submit Query** only after a return group is selected, followed by **Back to Main Menu**.
- Render the selected database and selected return-group label in the menu title or status text so the operator can see the two independent selections. Pressing Escape in either picker should preserve the prior selection and return to its parent menu.
- If the operator chooses a different database, clear the selected return group before allowing a query, unless a documented implementation invariant proves the group applies to the new database. This prevents stale fields from silently being sent to another dataset.
- Submit the exact configured field list with the selected database and query. Returning from `SearchResultsPager` or `SearchFieldsPager` must retain the selected database and return group.
- If the catalog is empty/invalid, report the configuration problem and keep **Submit Query** unavailable. Do not fall back to an unfiltered result set because that would defeat the requested field-set control.

### Alternatives considered

- **Put fields directly in `InteractiveISearchFlow`:** rejected because it duplicates configuration knowledge and makes field-set changes require code edits; a catalog keeps configuration parsing and validation in one boundary.
- **Reuse `SearchField` discovery to build return sets:** rejected because the request explicitly establishes `iSearchReturnTypes` as the source of configured groups, and live discovery should not silently replace an operator-authored field order.
- **Make `fl` optional and query without a selection:** rejected for the interactive path because it would allow the new menu choice to be bypassed and produce inconsistent return shapes.
- **Add a second nested workflow below database selection:** unnecessary; the existing dataset menu can retain its state and add one focused picker without changing the main-menu route.

## 7. Implementation steps

1. **Add the return-group configuration boundary.**
   - Create the focused definition/catalog and validator in `src/Carrot.Cli/Configuration/`, following the existing options and validation conventions.
   - Bind `iSearchReturnTypes` in `src/Carrot.Cli/Composition/ServiceRegistration.cs`; keep the current `appsettings.json` `Grants.DefaultFields` entry as the checked-in example and document that sibling keys add groups.
   - Verify valid ordering and exact field names, missing/empty groups, blank fields, duplicate fields, and absent configuration. Confirm invalid return configuration does not affect non-iSearch host startup.

2. **Carry and serialize the selected field set.**
   - Update `src/Carrot.Cli/ISearch/Contracts/SearchRequest.cs` and the related XML contract documentation with the ordered return fields.
   - Update `src/Carrot.Cli/ISearch/ISearchApiClient.cs` to validate the list and append an encoded `fl` parameter after the existing search controls.
   - Update `src/Carrot.Cli/ISearch/IISearchApiClient.cs` documentation if the request contract wording changes; do not alter the interface shape beyond what is necessary for the request object.
   - Extend `tests/Carrot.Cli.Tests/ISearch/ISearchApiClientTests.cs` with a synthetic request asserting GET method, selected dataset, query controls, exact field order, correct comma-separated/encoded `fl`, and no request for empty or invalid fields. Keep credentials synthetic and ensure failure messages never contain them.

3. **Add the return-dataset selector to the interactive flow.**
   - Update `src/Carrot.Cli/Cli/UI/InteractiveISearchFlow.cs` to inject the validated catalog, retain `selectedReturnDataset`/return-group state, add the new menu choice immediately after database selection, and gate **Submit Query** on both selections.
   - Pass the selected group's `DefaultFields` into `SearchRequest` in `submitQueryAsync` while leaving **View Fields** tied to the selected live database.
   - Handle changing databases, Escape, Back, empty configuration, field-set failures, results return, and cancellation without leaking config values or losing valid parent-menu state.
   - Update `tests/Carrot.Cli.Tests/ISearch/InteractiveISearchFlowTests.cs` and its fake client/setup to cover menu order, configured group labels, query blocking before group selection, exact fields sent for `Grants`, retained state after results/field discovery, changing databases, and cancellation. Adjust existing keyboard sequences for the inserted menu action.

4. **Update operator documentation.**
   - Update `src/Carrot.Cli/Docs/isearch.md` with the `iSearchReturnTypes` JSON shape, the **Select Return Dataset** step, the relationship between the live database and configured return group, the exact `fl` behavior, and configuration failure guidance.
   - Update `src/Carrot.Cli/Docs/getting-started.md`, `docs/cli-reference.md`, and the relevant `README.md` iSearch summaries so they no longer imply that every query uses an implicit/unfiltered result shape.
   - Keep examples synthetic or use the existing non-secret field names; never include an API key, cookie value, or user secret in documentation, fixtures, logs, or assertions. Preserve the embedded-help resource workflow.

5. **Verify the complete feature.**
   - Run focused configuration, iSearch API, interactive-flow, and related menu tests first.
   - Run `dotnet build .\Carrot-CLI.slnx --no-restore`, `dotnet test .\Carrot-CLI.slnx --no-build --no-restore`, `dotnet format .\Carrot-CLI.slnx --verify-no-changes --no-restore`, and `git diff --check`.
   - Confirm the project still emits XML documentation and that the changed C# members retain the repository's required documentation header and `#region implementation` conventions. Verify the iSearch help topic renders the new configuration and menu sequence.
   - If a configured-key smoke test is available, select a live database and the matching configured return group, submit a harmless bounded query, and inspect that the request contains the expected fields while no credential appears in terminal output. Do not issue network requests without the required User Secrets key and contact email.
   - Implementation completed and verified; the coordinating agent records one complete code-task entry in `C:\Source\Programs\Journal.md` covering every changed source, test, configuration-related, and code-related documentation file.

## 8. Acceptance criteria

- After a live database is selected, **Select Return Dataset** appears immediately beneath **Select Database** and lists only validated groups from `iSearchReturnTypes`.
- **Submit Query** is unavailable until both a live database and a configured return group are selected; no unfiltered fallback query is sent.
- Selecting `Grants` uses its configured `DefaultFields` in the original order and sends them as iSearch's `fl` result-field list on `GET /search/{dataset}`.
- Field names are URI-safe, credentials remain cookie/header-only, and existing iSearch timeout, cancellation, retry, redirect, pacing, and bounded-error behavior is unchanged.
- Returning from results or field discovery retains the selected database and return group; changing the database cannot leave an incompatible stale field set active.
- Missing, empty, duplicate, or malformed return-group configuration is reported safely and does not break unrelated CLI commands.
- Unit/interactive tests cover configuration validation, menu ordering and gating, exact request serialization, state retention/reset, failures, Escape/Back, and cancellation.
- The full build, test suite, format check, whitespace check, and documentation/help verification pass, with any unavailable live-service smoke test explicitly reported.

## 9. Risks and assumptions

- The existing `Grants.DefaultFields` shape is the intended schema, and its child key is an operator-facing return-group label rather than a required live iSearch database mapping. If groups must be tied to specific databases, a separate configuration requirement is needed before implementation because the current JSON has no mapping property.
- iSearch field names are dataset-specific and service-owned. This plan sends configured names exactly as written and relies on the sanitized API response for invalid-field feedback; it does not infer compatibility from field discovery.
- The phrase “return dataset” is interpreted as the user-facing name for a configured return field group. The live database remains selected separately from `/datasets`.
- Existing tests currently assume that selecting a database makes **Submit Query** immediately available; their input queues and expected `SearchRequest` values will need to be updated for the new required selection.
- Multiple groups may use different field sets, but no persistence or default-group memory is introduced; each iSearch visit starts with no selected database or return group.

## 10. Deferred follow-up

- Per-group database restrictions, if the product later requires a configured group to be valid only for a specific live database.
- Interactive editing or ad hoc selection of individual fields.
- Automatic live-schema validation, field autocomplete, field-type-aware query controls, and dynamic field-set generation.
- Named iSearch commands, exports, saved searches, and persisted return-group preferences.
