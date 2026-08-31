# Save Processed Results to Excel

Status: Done

## 1. Outcome

Add a **Save Processed Results to Excel** item to the retained **Prepared Batch Actions** menu. After a successful **Process Prepared Items** run, an operator can choose an explicit `.xlsx` destination and save the currently retained processed result as one atomic, formula-safe workbook whose `Results` worksheet follows the repository's documented 23-column schema.

The existing interactive preparation and API-processing behavior is the validated baseline. The preceding API milestone has been smoke-tested successfully, so this change must preserve its request, response-correlation, paging, retry, and retained-result behavior while adding an opt-in file-writing action.

## 2. Problem

The interactive workflow now retains a complete successful `ProcessedDocumentBatch`, but `ProcessDocumentsMenu` only permits viewing that result in memory. The menu and documentation still state that no files are written. The repository already contains the intended reporting contracts (`IExcelReportWriter`, `ReportRequest`, and `ReportRow`), ClosedXML references, a reusable `AtomicFileWriter` seam, and skipped workbook acceptance tests, but the actual writer and atomic persistence method still throw `NotImplementedException("Layout stub only.")` and are not registered with dependency injection.

This leaves operators unable to keep, share, or analyze the processed result after leaving the menu. A direct writer call from the menu would also mix prompting, result mapping, workbook formatting, filesystem safety, and error translation in one UI class, so the export needs a small application boundary rather than more orchestration inside `ProcessDocumentsMenu`.

## 3. Solution Vision

- Show **Save Processed Results to Excel** immediately after **View Processed Results**, and only while `latestProcessedBatch` contains a complete success.
- Ask for one quoted or unquoted output file path. Normalize relative paths against the current working directory, require a `.xlsx` extension and an existing parent directory, and never write merely because processing succeeded.
- When the destination exists, ask for explicit overwrite confirmation with a default of **No**. Declining returns to Batch Actions without modifying the file.
- Convert the retained batch to the established `ReportRow` schema through a focused mapper/export service. One export contains one row per submitted document, in `CarrotDocumentIndex` order, including explicit unassigned rows.
- Generate a stable nonempty `RunId` through an injected run-ID provider when a `ProcessedDocumentBatch` is first created so viewing and repeated saves of the same retained result use the same correlation identifier. A later successful reprocess receives a new identifier; a failed reprocess retains the prior batch and identifier.
- Write a single worksheet named `Results` through ClosedXML, force all string cells to the text data type, retain numeric and Boolean cell types where appropriate, and promote a complete temporary sibling file to the destination only after workbook generation succeeds.
- On success, display the normalized absolute path. On validation or expected filesystem/reporting failure, show a concise safe error and keep the processed batch available for viewing, retrying, or saving elsewhere.

This uses the existing report schema and atomic writer because they are already the repository's intended persistence boundaries. A menu-specific workbook shape or direct ClosedXML work in the UI would duplicate those contracts and make the future named `process` command diverge.

## 4. Scope

### In scope

- The conditional interactive menu item, output-path prompt, overwrite confirmation, success/failure feedback, and retained-state behavior.
- A path validator/normalizer dedicated to output files; input-path validation is not reused because output files do not exist yet.
- Stable run identity on successful `ProcessedDocumentBatch` values.
- Mapping successful processed rows and their source metadata, content previews, assignment state, category paths, scores, and membership JSON into the existing `ReportRow` model.
- Implementation and DI registration of `AtomicFileWriter`, `IExcelReportWriter`, and a processed-results export boundary.
- One atomic `.xlsx` artifact with the documented `Results` schema and practical worksheet formatting.
- Automated tests for every new or changed public production method, plus mapper, atomic-file, menu, registration, and documentation behavior.
- Updates to user-facing documentation and help that currently say the interactive workflow writes no files.

## 5. Non-goals

- Implementing the deferred noninteractive `process`, `preview`, or `server-info` command bodies.
- Writing request/response JSON sidecars, logs, CSV files, multiple worksheets, charts, or summary dashboards.
- Exporting failed preparation rows. This action saves the latest processed result, and only successfully prepared documents were submitted to Carrot. Failed preparation rows remain visible in **View Prepared Results**.
- Automatically saving after processing, choosing an output location without operator input, remembering a prior output path, or creating a missing destination directory.
- Reprocessing documents, rereading source files, contacting Carrot, or changing the retained response during export.
- Changing the documented exit-code behavior of deferred named operational commands.

## 6. Technical Approach

### Interactive export boundary

- Add a `SaveProcessedResults` member to `ProcessDocumentsMenu.BatchChoice`. Insert it only when `latestProcessedBatch` is non-null, directly after `ViewProcessedResults`, and render it as **Save Processed Results to Excel**.
- Inject one focused `ProcessedResultsExportFlow` into `ProcessDocumentsMenu`. The flow owns the Spectre.Console path prompt, overwrite confirmation, and feedback and depends on `ExcelOutputPathResolver` plus `IProcessedResultsExporter`. This prevents the already dependency-heavy parent menu from directly acquiring multiple persistence concerns.
- Add an internal `SaveProcessedResultsRequest` containing the retained batch, normalized destination path, and overwrite choice. Add `IProcessedResultsExporter.SaveAsync(...)` returning an `OperationResult<string>` whose success value is the absolute saved path.
- Treat `IOException`, `UnauthorizedAccessException`, and workbook/argument failures as structured export failures with stable message codes and safe messages. Do not catch caller cancellation as an ordinary failure. Unexpected programmer faults should remain visible to tests rather than being broadly swallowed.

### Output-path behavior

- Add an `ExcelOutputPathResolver` that trims whitespace, removes only matching surrounding single or double quotes, resolves an absolute path with `Path.GetFullPath`, requires `.xlsx` case-insensitively, rejects a directory target, and requires an existing parent directory.
- Reuse `OperationResult<string>` for validation messages so prompt validation and save execution share the same rules.
- Check `File.Exists` only to decide whether the menu should ask for overwrite confirmation. Enforce `Overwrite` again in the atomic writer to close the prompt-to-write race.

### Stable report mapping

- Add `RunId` to `ProcessedDocumentBatch`. Introduce a lean `IRunIdProvider`/system implementation and have `PreparedDocumentProcessor` request an ID only after `/list`, `/cluster`, response validation, and membership mapping have all succeeded. Keep the value with the retained batch and replace the provider with a deterministic fake in tests.
- Add a hand-written `ProcessedDocumentReportMapper` that creates `ReportRequest.Rows` in deterministic submitted order. The transformation contains schema-specific truncation, score, and membership rules, so an explicit mapper is clearer and cheaper than introducing AutoMapper or Mapperly for this single boundary. Populate:
  - `RunId`, `RunStatus = "Success"`, endpoint, request algorithm/language, and a null template.
  - Source ordinal, Carrot index, container/relative/file paths, extension, size, and SHA-256 from `PreparedDocument` and `SourceFile`.
  - `ExtractionStatus = "Ready"`, a null error, exact character count, the first `ContentPreviewCharacterLimit` characters from the complete retained content, and the correct truncation flag. Do not use the shorter console preview.
  - Membership count, newline-aligned category paths and invariant round-trip score text, and deterministic JSON for every flattened membership. A null score contributes an empty aligned score line. Unassigned rows use zero/blank display values and an empty JSON array.
- Take the 30,000-character limit from validated `CarrotCliOptions` so configuration and workbook behavior cannot drift.

### Workbook and atomic persistence

- Implement `AtomicFileWriter.WriteAsync` with a uniquely named temporary file in the destination directory, `FileMode.CreateNew`, cooperative cancellation, flush before promotion, overwrite enforcement, and best-effort cleanup of the exact temporary sibling on success, failure, or cancellation. A failed write must leave an existing destination byte-for-byte unchanged and must not leave a temporary artifact.
- Inject `AtomicFileWriter` into `ExcelReportWriter`. Validate the request, then create an `XLWorkbook` with exactly one `Results` sheet and the 23 headers already documented in `docs/output-format.md` and `src/Carrot.Cli/Docs/output-columns.md`.
- Write rows in request order. Set every workbook-bound string cell explicitly to `XLDataType.Text` so leading `=`, `+`, `-`, or `@` values remain inert and display unchanged. Write integers, longs, and Booleans as native values.
- Freeze the header row, enable filters, bold/style the header, wrap the content and membership columns, and apply bounded useful widths rather than auto-sizing against 30,000-character content. Preserve line breaks in paths/scores and do not add formulas, links, or external workbook connections.
- Check cancellation before expensive workbook work and during row emission. Save the workbook into the atomic writer's temporary stream, then allow the atomic writer to promote it.

### Composition and documentation

- Register the run-ID provider, `AtomicFileWriter`, `IExcelReportWriter`/`ExcelReportWriter`, the report mapper, output-path resolver, `IProcessedResultsExporter`/implementation, and `ProcessedResultsExportFlow` in `ServiceRegistration.AddCarrotCli`. Choose singleton lifetimes for stateless helpers/writers and transient lifetimes for the exporter/flow/menu orchestration.
- Update XML documentation and remarks that currently call reporting deferred or state that the interactive workflow never writes files.
- Update `README.md`, `docs/cli-reference.md`, `docs/application-preamble.md`, `docs/output-format.md`, `docs/troubleshooting.md`, and the relevant embedded help topics (`getting-started`, `process`, `privacy`, `output-columns`, and `troubleshooting`). State clearly that export is explicit, the workbook includes source paths/hashes and up to 30,000 characters of extracted content per submitted document, no request/response sidecars are written, and failed preparation rows are not part of this processed-results export.

## 7. Implementation Steps

1. **Add stable processed-run identity.**
   - Files/symbols: `src/Carrot.Cli/Processing/ProcessedDocumentBatch.cs`, `PreparedDocumentProcessor.ProcessAsync`, new `IRunIdProvider.cs`/`SystemRunIdProvider.cs`, and `tests/Carrot.Cli.Tests/Processing/PreparedDocumentProcessorTests.cs` plus a focused provider test.
   - Change: add `RunId`, request it only on complete success, and verify it is the deterministic injected value, changes when the provider supplies a later value, and remains the same object/value when the menu retains a prior success after failure.
   - Why: the existing workbook contract requires a run correlation value and repeated exports of one retained result must agree.
   - Verify: processor tests cover the changed public `ProcessAsync` behavior and its constructor remains covered, including argument guards if the signature changes.

2. **Implement deterministic processed-result-to-report mapping.**
   - Files/symbols: new `src/Carrot.Cli/Reporting/ProcessedDocumentReportMapper.cs`; existing `ReportRequest.cs`, `ReportRow.cs`, and `CarrotCliOptions.cs` only if documentation or validation needs adjustment; new `tests/Carrot.Cli.Tests/Reporting/ProcessedDocumentReportMapperTests.cs`.
   - Change: map source metadata, full retained-content previews, truncation boundaries, request settings, assigned/unassigned memberships, aligned invariant scores, and deterministic membership JSON without rereading disk.
   - Why: isolates the wire/result model from ClosedXML and makes the schema independently testable.
   - Verify: test zero, 29,999, 30,000, and 30,001-character content; source-ordinal gaps; overlapping/nested memberships; null scores; unassigned rows; non-English current culture; and formula-looking strings.

3. **Implement safe output-path resolution.**
   - Files/symbols: new `src/Carrot.Cli/Reporting/ExcelOutputPathResolver.cs` and `tests/Carrot.Cli.Tests/Reporting/ExcelOutputPathResolverTests.cs`.
   - Change: support matching quotes and relative/absolute paths while rejecting malformed quotes, non-`.xlsx` targets, directories, invalid paths, and missing parent directories.
   - Why: input normalization assumes an existing input and therefore cannot safely validate a new output file.
   - Verify: cover quoted paths with spaces, mixed-case `.XLSX`, relative normalization, invalid extensions/characters, directory targets, and nonexistent parents.

4. **Implement atomic file persistence.**
   - Files/symbols: `src/Carrot.Cli/Common/AtomicFileWriter.cs` and new `tests/Carrot.Cli.Tests/Common/AtomicFileWriterTests.cs`.
   - Change: replace the layout stub with same-directory temporary writing, flush/promotion, no-overwrite and overwrite paths, cancellation propagation, and exact-temp cleanup.
   - Why: an interrupted or failed workbook save must never expose a partial final file or damage an existing report.
   - Verify: assert new writes, atomic replacement, existing-file preservation when overwrite is false, callback failure/cancellation preservation, no orphan temporary files, and argument validation.

5. **Implement the ClosedXML workbook.**
   - Files/symbols: `src/Carrot.Cli/Reporting/ExcelReportWriter.cs`, `IExcelReportWriter.cs`, and `tests/Carrot.Cli.Tests/Reporting/ExcelReportWriterTests.cs`.
   - Change: inject the atomic writer, replace both skipped layout acceptances with active asynchronous tests, emit the exact worksheet/header contract, write safe typed cells, and apply bounded formatting.
   - Why: activates the existing report seam without creating a second workbook design.
   - Verify: reopen generated files with ClosedXML and assert the sole sheet, exact ordered headers, row order/count, values and data types, text/formula safety, 30,000-character values, line breaks, formatting essentials, overwrite behavior, cancellation, and absence of temp files. Every public production method on the writer receives direct coverage.

6. **Add the processed-results exporter.**
   - Files/symbols: new `IProcessedResultsExporter.cs`, `ProcessedResultsExporter.cs`, and `SaveProcessedResultsRequest.cs` under `src/Carrot.Cli/Reporting`; new `ProcessedResultsExporterTests.cs`.
   - Change: validate the request, invoke the mapper and writer, return the absolute output path on success, translate expected write failures into structured messages, and propagate cancellation.
   - Why: keeps application/error policy out of both the UI and ClosedXML implementation and provides a reusable boundary for later command work.
   - Verify: cover success request forwarding, overwrite forwarding, mapper/writer failure reporting, cancellation, and every public interface/implementation method.

7. **Add the export prompt flow and conditional menu action.**
   - Files/symbols: new `src/Carrot.Cli/Cli/UI/ProcessedResultsExportFlow.cs`; `src/Carrot.Cli/Cli/UI/ProcessDocumentsMenu.cs`; new focused flow tests and existing `tests/Carrot.Cli.Tests/Cli/ProcessDocumentsMenuTests.cs`.
   - Change: let the flow prompt/validate the path, confirm overwrite, invoke the exporter, and render saved-path or safe failure feedback. Inject only the flow into the parent menu, conditionally add its choice, and preserve `latestProcessedBatch` after saves, declines, and failures.
   - Why: completes the requested opt-in workflow while preserving separation between parent-menu navigation, export interaction, report mapping, and file persistence.
   - Verify: focused Spectre `TestConsole` state-transition cases cover invalid-path reprompting, one new-file save, overwrite decline/no write, overwrite acceptance forwarding `true`, safe failure feedback, and cancellation. Parent-menu cases prove the item is absent before a success and present afterward, the exact retained batch reaches the flow, errors retain the menu/result, Escape/back behavior remains intact, and saving does not invoke the API-processing path again.

8. **Register services and update composition tests.**
   - Files/symbols: `src/Carrot.Cli/Composition/ServiceRegistration.cs` and `tests/Carrot.Cli.Tests/Cli/CommandRouteTests.cs` or a focused new composition test.
   - Change: register and resolve the complete interactive export graph without registering deferred `DocumentProcessingWorkflow` or JSON artifact writing.
   - Why: production menu construction must receive the implemented services while deferred workflows remain untouched.
   - Verify: build a host/service provider, resolve `ProcessDocumentsMenu` and each export abstraction, and confirm intended lifetimes where material.

9. **Bring documentation and status assertions in line with the feature.**
   - Files/symbols: `README.md`; `docs/application-preamble.md`; `docs/cli-reference.md`; `docs/output-format.md`; `docs/troubleshooting.md`; `src/Carrot.Cli/Docs/getting-started.md`; `src/Carrot.Cli/Docs/process.md`; `src/Carrot.Cli/Docs/privacy.md`; `src/Carrot.Cli/Docs/output-columns.md`; `src/Carrot.Cli/Docs/troubleshooting.md`; relevant assertions in `CommandRouteTests`, `InteractiveMenuTests`, and `ProcessDocumentsMenuTests`.
   - Change: replace unconditional “no files are written” language with explicit-save behavior and document path, overwrite, privacy, workbook contents, and no-sidecar boundaries.
   - Why: exporting content and local identifiers is a privacy-relevant behavior and embedded help is the deployed user contract.
   - Verify: route/help tests find the new menu and guidance, and no active documentation still describes Excel as unavailable for the interactive processed-results workflow.

10. **Run repository verification and complete the plan record.**
    - Run `dotnet build .\Carrot-CLI.slnx --no-restore --disable-build-servers -m:1 --verbosity minimal`, `dotnet test .\Carrot-CLI.slnx --no-build --no-restore --verbosity normal`, the established publish check if packaging files change, and `git diff --check`.
    - Perform a manual smoke test using the already validated local processing flow: process a small batch, save a new workbook, inspect it in Excel/LibreOffice, decline and then accept overwrite, and confirm the processed result remains viewable.
    - Add the requested completion journal entry with EST timestamp and verification evidence, then rename this file from `(pending) Save-Processed-Results-to-Excel-Plan.md` to `(done) Save-Processed-Results-to-Excel-Plan.md` only after implementation, automated verification, and the export smoke test pass.

## 8. Acceptance Criteria

- Before any successful processing result exists, Batch Actions does not offer Excel export.
- After success, Batch Actions offers **Save Processed Results to Excel** next to the processed-results view action, and saving never calls Carrot or rereads source files.
- The path prompt accepts supported quoting/relative paths, rejects invalid or non-`.xlsx` destinations, and requires confirmation before replacing an existing file.
- Declining overwrite or encountering a validation/write failure leaves the destination and retained processed batch unchanged.
- A successful save produces exactly one openable `.xlsx` file, reports its absolute path, and leaves no temporary sibling.
- The workbook contains one `Results` worksheet and the exact documented 23 headers in order, with one row per submitted document in Carrot index order, including unassigned documents.
- Run/source/request/content/membership fields map correctly; content previews stop at the configured 30,000-character limit; scores use invariant unrounded text aligned to category paths; membership JSON is deterministic.
- All workbook-bound strings are stored as text and formula-looking source data remains inert and visibly unchanged.
- Saving the same retained result more than once preserves its `RunId`; a later successful processing run has a different `RunId`; a failed reprocess does not replace the earlier ID or result.
- Workbook creation and replacement are atomic under success, overwrite, exception, and cancellation paths.
- No JSON sidecar or log is produced by this menu action.
- Every new or changed public production method has direct automated test coverage; the complete test suite, build, whitespace check, and manual export smoke test pass.
- User documentation accurately describes the explicit export and its privacy implications.

## 9. Risks and Assumptions

- `ProcessedDocumentBatch` retains complete extracted text and flattened membership data, so export can be deterministic without touching the original files or server. The workbook intentionally includes at most 30,000 characters per submitted document rather than the full text.
- Excel permits 32,767 characters per cell; the existing 30,000-character default remains below that boundary. Configuration validation must continue preventing an unsafe larger value.
- Membership score strings must use invariant round-trip formatting; current-culture formatting could misalign or alter values across machines.
- ClosedXML operations are synchronous and can be memory-intensive for large batches. Cooperative checks can stop row construction between documents, but cancellation cannot interrupt every internal ClosedXML operation. Atomic promotion still prevents a partial destination.
- Atomic replacement is reliable only when the temporary file is created beside the destination. Network shares and files locked by Excel may reject promotion; these are reported safely and leave the retained batch available.
- Formula safety depends on explicitly assigning text cell types for every untrusted string, not merely prefixing apostrophes or relying on ClosedXML inference.
- The menu's pre-write existence check is advisory. The atomic layer is authoritative if another process creates or changes the destination between confirmation and promotion.
- This phase assumes the existing 23-column schema remains the desired shared contract. Failed preparation rows and exact raw request/response artifacts are deferred because the requested action is specifically for the retained processed result.

## 10. Deferred Follow-up

- Reuse the report mapper/writer in the future named `process` command, including its `--output` and `--overwrite` options.
- Decide whether named processing should include failed extraction rows in the same workbook and how `PartialSuccess` should populate `RunStatus`.
- Implement optional exact `.request.json` and `.response.json` sidecars and log artifacts through the existing deferred reporting seams.
- Consider a configurable/default output directory and remembered interactive destination after real operator feedback; this milestone deliberately requires an explicit path.
- Evaluate streaming or lower-memory workbook generation only if measurement shows ClosedXML memory use is problematic at documented safeguard limits.
