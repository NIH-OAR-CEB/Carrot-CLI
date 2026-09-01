# Named Request Preview

Status: Complete

Category: Operational commands

Depends on: 01 Named Command Foundation, 02 JSON Artifact Persistence, 03 Clustering Selection and Parameters

## 1. Outcome

Make `carrot-cli preview` discover and extract input documents, validate clustering configuration, and atomically write the exact request JSON without calling `/cluster` or prompting.

## 2. Problem

`PreviewCommand` and `DocumentProcessingWorkflow.PreviewAsync` are layout stubs. Interactive Process Documents can preview an already prepared package in memory, but there is no unattended preview that starts from `--input` and creates the promised output artifact.

## 3. Solution Vision

Reuse the implemented preparation workflow and shared `ClusterRequestFactory`, then validate the resolved clustering selection through `/list`. The preview workflow will persist exactly the request that a corresponding process run would submit and return a structured `ProcessRunResult` for reporting and exit-code mapping.

## 4. Scope

- Implement `PreviewCommand` and `DocumentProcessingWorkflow.PreviewAsync`.
- Support documented input, recursion, endpoint, clustering, parameter, timeout, output, and overwrite options.
- Write one complete request JSON artifact atomically.
- Preserve partial-success semantics for file-level extraction failures and never invoke `/cluster`.

## 5. Non-goals

- Excel reports, response sidecars, interactive Preview Request, or clustering submission.
- File logging beyond integration with the separately implemented logger when available.

## 6. Technical Approach

- Refactor `DocumentProcessingWorkflow` to depend on `IDocumentPreparationWorkflow` instead of duplicating discovery/extraction logic.
- Use `/list` only for exact selection validation and `ClusterRequestFactory` for payload identity.
- Derive a deterministic output file when `--output` is a directory; require explicit overwrite for existing artifacts.
- Return `0` for complete success, `2` for preview with file-level failures, `3` for no ready documents/input failure, `4` for `/list`, `6` for artifact failure, and `130` for cancellation.

## 7. Implementation Steps

1. Implement the preview branch of `DocumentProcessingWorkflow` using preparation, configuration validation, request creation, and JSON persistence services.
2. Implement `PreviewCommand` constructor/execution and production DI registration.
3. Activate the deferred preview workflow acceptance and add command-harness state/exit-code coverage.
4. Prove through a fake API client that `/cluster` is never called and the saved JSON equals the shared request object.
5. Update README, CLI reference, Task Scheduler, privacy, exit-code, preview, and troubleshooting documentation.

## 8. Acceptance Criteria

- Preview accepts the documented named options and never prompts.
- It discovers supported files in deterministic order and writes the complete request JSON.
- It may call `/list` for validation but never calls `/cluster`.
- Expected file-level failures produce partial success while no-ready-input and artifact failures use stable exit codes.
- All changed production methods are covered; build, tests, formatting, publish, and whitespace verification pass.

## 9. Risks and Assumptions

- Previewing large extracted content creates a correspondingly large JSON file by design.
- Request equivalence must be tested structurally and through shared serialization settings to prevent drift.

## 10. Deferred Follow-up

- Named processing and the interactive main-menu Preview Request.

## 11. Completion Evidence

- `DocumentProcessingWorkflow.PreviewAsync` now composes shared preparation, clustering resolution, `/list` validation, `ClusterRequestFactory`, and atomic JSON persistence. It creates no cluster submission, preserves usable partial preparation, and maps input, configuration, endpoint, output, and cancellation outcomes to stable run results.
- `PreviewCommand` resolves named settings without prompting, reports normal terminal results, and is registered with the production workflow graph. Omitted output uses `<input-parent>\<input-name>.request.json`; an existing output directory receives that same input-derived filename.
- Focused fake-boundary tests prove the saved request is structurally identical to the shared factory result and that `/cluster` is never called. Command-harness coverage verifies resolution, output reporting, invalid configuration, and cancellation handling.
- README, CLI reference, Task Scheduler, embedded help, privacy, exit-code, output, preview, and troubleshooting guidance now document active named preview behavior while retaining interactive Preview Request and named process as deferred work.
