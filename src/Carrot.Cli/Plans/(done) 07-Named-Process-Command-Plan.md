# Named Process Command

Status: Complete

Category: Operational commands

Depends on: 01 Named Command Foundation, 02 JSON Artifact Persistence, 03 Clustering Selection and Parameters, 04 Command Logging and Quiet Output, 06 Named Request Preview

## 1. Outcome

Make `carrot-cli process` run unattended from input discovery through one global Carrot clustering request and atomic report/artifact persistence with stable Task Scheduler exit codes.

## 2. Problem

The named process route now composes existing preparation, global clustering, response correlation, Excel reporting, JSON artifact persistence, reporting, and logging boundaries without prompting.

## 3. Solution Vision

Compose existing validated boundaries rather than reimplementing them: prepare inputs, create the same previewable request, validate `/list`, submit once through `IPreparedDocumentProcessor` or a shared processor, map memberships, write Excel through the established report pipeline, optionally write exact request/response sidecars, report/log results, and return the documented exit code.

## 4. Scope

- Implement `ProcessCommand` and `DocumentProcessingWorkflow.ProcessAsync`.
- Support every documented process option, including output, overwrite, JSON suppression, quiet mode, and log file.
- Preserve deterministic source order, one global request, partial-success file handling, correlation, and atomic output guarantees.
- Register and verify the complete unattended command graph.

## 5. Non-goals

- Interactive UI changes, CSV/multiple-sheet output, scheduled-task creation, or automatic retry of nontransient failures.
- Splitting large batches into multiple clustering calls.

## 6. Technical Approach

- Reuse `IDocumentPreparationWorkflow`, the shared clustering configuration/request services, `ICarrotApiClient`, `ClusterMembershipMapper`, `ProcessedDocumentReportMapper`, `IExcelReportWriter`, and `IJsonArtifactWriter`.
- Generate one run ID and use it consistently across workbook rows, artifacts, console output, and logs.
- Stage all outputs and define failure policy so incomplete artifact sets are reported clearly; never expose a partial individual file.
- Preserve the existing overall timeout and transient retry semantics.

## 7. Implementation Steps

1. Implement the processing branch of `DocumentProcessingWorkflow` around existing preparation, API, mapping, and reporting boundaries.
2. Define deterministic output and sidecar filenames for file and directory `--output` forms.
3. Implement `ProcessCommand` constructor/execution, cancellation handling, reporter/logger integration, and DI registration.
4. Activate all deferred workflow acceptance cases for global request semantics, source correlation, persistence, failures, retries, cancellation, and exit codes.
5. Add end-to-end fake-HTTP tests using mixed supported fixtures and real workbook/JSON writers.
6. Update all command, Task Scheduler, output, privacy, troubleshooting, and exit-code documentation.

## 8. Completion Evidence

- Implemented `ProcessCommand`, DI registration, and `DocumentProcessingWorkflow.ProcessAsync` with stable exit-code and cancellation behavior.
- Reused one `IPreparedDocumentProcessor` call for global clustering and retained its run ID in workbook rows, artifact paths, and command results.
- Added process workflow coverage for complete, partial, and `/list` failure outcomes, and updated operational documentation for workbook and sidecar behavior.

## 8. Acceptance Criteria

- The command never prompts and is suitable for redirected execution and Task Scheduler.
- All ready documents are sent in one request identical to named preview for the same resolved settings.
- Excel and optional request/response JSON artifacts are complete, atomic, correlated by one run ID, and formula-safe.
- `--no-json-artifacts`, `--overwrite`, `--quiet`, and `--log-file` behave as documented.
- Complete, partial, input, endpoint, cluster, persistence, and cancellation outcomes return codes `0`, `2`, `3`, `4`, `5`, `6`, and `130` as applicable.
- Every changed production method is covered; build, full tests, formatting, publish, whitespace checks, and an opt-in local Carrot smoke test pass.

## 9. Risks and Assumptions

- Cross-artifact all-or-nothing transactions are not available across filesystem files; the run result must identify exactly which atomic artifacts completed.
- Memory requirements remain bounded by existing safeguards but one global clustering request is intentionally retained.

## 10. Deferred Follow-up

- Interactive parity and any future scheduled-task installation helper.
