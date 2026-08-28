# Process Documents Preparation and Review Menu

## Summary

Replace menu option 1's generic `Pending Implementation` screen with an interactive preparation workflow that accepts multiple files, folders, and ZIP archives, extracts their content, and displays the results in a paged table.

This milestone stops before Carrot submission and artifact generation. `Process Prepared Items` will be visible but will display a clear pending message without contacting a server or writing output. Preview, Server Information, and named operational commands remain deferred.

## Interaction and Behavior

- The Process Documents setup menu provides Add Path, Remove Path, Prepare, Help, and Back.
- Add one path per prompt. Accept unquoted paths and matching single- or double-quoted paths, trim surrounding whitespace/quotes, and resolve relative paths against the current working directory.
- Reject unmatched quotes, nonexistent paths, unsupported individual files, and paths other than directories, `.zip` archives, or supported documents.
- When folders are present, ask once whether all folders should include subdirectories; default to nonrecursive discovery.
- Keep paths editable until Prepare is selected. Require at least one path.
- Process inputs in entered order and files within each folder/archive by normalized relative path. Assign one global source ordinal.
- Deduplicate canonical source identities case-insensitively, retaining the first occurrence and reporting later occurrences as warnings. Identical content at genuinely different paths remains separate.
- Immediately after preparation, show a summary and the first results page. Retain the in-memory batch until Start Over, confirmed return to the main menu, or cancellation.
- Show five rows per page with ordinal, readiness status, relative/source path, extension, formatted size, extracted character count, text preview, and concise warning/error.
- Limit terminal previews to 120 characters, collapse whitespace and line breaks, indicate truncation, and escape all paths, extracted text, and messages before rendering.
- Provide Previous Page, Next Page, and Back to Batch Actions controls. Map Escape to Back to Batch Actions using Spectre.Console's cancel-result support.
- Batch actions provide View Results, Preview JSON Package, Process Prepared Items, Start Over, Help, and Back to Main Menu.
- Preview JSON Package uses the shared Carrot request-contract mapper to render configured language/algorithm values and every ready document's complete title/content as indented, screen-sized pages. Long strings use a display-only `↪` continuation marker; Next is the default, Previous moves backward, and Escape returns to batch actions. It excludes client-only metadata, performs no network request, and writes no file.
- `Process Prepared Items` is enabled when at least one row is ready. It reports the ready count and that processing is pending, performs no network/output work, and retains the batch. When no rows are ready, show the action as unavailable with an explanation.
- Failed rows remain reviewable but are excluded from the future processable document collection. Start Over and Back require confirmation before discarding a prepared batch.
- Ctrl+C propagates cancellation, cleans temporary ZIP content, and returns exit code `130`.

## Implementation Changes

- Split Process Documents into a dedicated `ProcessDocumentsMenu`; keep the existing generic deferred submenu for Preview and Server Information. Add a focused prepared-results pager/renderer and route `InteractiveMenu` option 1 to it.
- Add internal `PrepareDocumentsRequest`, `PreparedDocumentBatch`, `PreparedDocumentRow`, and `PreparedDocumentStatus` models plus `IDocumentPreparationWorkflow.PrepareAsync`. Return `OperationResult<PreparedDocumentBatch>`; the batch exposes all display rows and a separate ordered collection of successfully extracted `ExtractedDocument` values.
- Define aggregate outcomes as `Success` when at least one document is ready and none were skipped or failed, `PartialSuccess` when at least one is ready with warnings/failures, and `Failure` when no documents are ready.
- Implement the existing operation-result factories with immutable messages and stable message codes for validation, duplicates, unsafe archives, extraction failures, and empty batches.
- Add a single-file loader and update the input resolver to classify supported files, folders, and ZIPs. Change loader failures to return structured operation results instead of using exceptions for expected input conditions.
- Centralize supported extensions so prompt validation, discovery, and extractor selection cannot drift.
- Implement deterministic folder discovery, direct-file loading, ZIP expansion/cleanup, global batching, and duplicate detection. Preserve existing safeguards for file counts/sizes, reparse points, Office temporary files, ZIP traversal, absolute entries, nested archives, compression ratios, and expanded size.
- Process input containers sequentially so each ZIP can be extracted, prepared, and cleaned before the next container; retain only prepared metadata and extracted text afterward.
- Implement the extraction coordinator, SHA-256 service, and TXT/Markdown, DOCX, XLSX, PPTX, and PDF extractors according to the documented extraction rules.
- Treat corrupt, encrypted, image-only, unreadable, and empty documents as file-level failures while continuing with valid documents. Assign contiguous Carrot document indexes only to successful rows and use the filename without its extension as the future document title.
- Bind and validate `CarrotCliOptions`; add configurable defaults of five prepared rows per page and a 120-character terminal preview.
- Register only the preparation/input/extraction services needed by menu option 1. Leave `DocumentProcessingWorkflow`, Carrot API, report writers, `ProcessCommand`, and `PreviewCommand` operationally deferred.
- Update embedded help, CLI reference, README, and application preamble with quoted-path examples, multiple-input behavior, paging/Escape controls, preview privacy, partial failures, and the pending Process handoff.

## Test Plan

- Activate and expand the existing input and extraction placeholder tests using deterministic documents and archives.
- Verify quoted/unquoted paths, relative resolution, invalid input, editable path-list behavior, global ordering, recursion, direct files, mixed inputs, and canonical deduplication.
- Verify folder safety, all ZIP defenses and limits, temporary cleanup, every extraction format, hashes, successful indexes, structured failures, bounded concurrency, and total-character limits.
- Verify aggregate operation statuses and successful-row-only processability.
- Use `TestConsole` to verify setup navigation, preparation, five-row page boundaries, preview normalization/truncation, markup escaping, Previous/Next behavior, Escape retention, confirmation flows, cancellation, and the pending Process action.
- Keep Preview and Server Information pending and prove the new menu performs no HTTP or report-output operation.
- Run build, the complete test suite, formatting verification, publish smoke testing, and whitespace checks.

## Assumptions and Defaults

- Prepared means discovered, validated, extracted, hashed, and retained in memory; no preparation artifact is written.
- One Add Path action accepts one path, while a batch may contain any mixture of supported files, folders, and ZIPs.
- Wildcards, environment-variable expansion, and multiple pasted paths in one prompt are not supported.
- The recursive choice applies uniformly to every folder in the batch.
- Full Carrot submission, endpoint/clustering prompts, Excel/JSON/log output, and named-command implementation are out of scope.
- OCR, nested ZIP processing, authentication, and persisted input batches remain out of scope.
