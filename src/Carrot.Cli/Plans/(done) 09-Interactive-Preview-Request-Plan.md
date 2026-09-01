# Interactive Preview Request

Status: Complete

Category: Interactive parity

Depends on: 06 Named Request Preview

## 1. Outcome

Replace the main-menu **Preview Request** `Pending Implementation` panel with an interactive input/configuration flow that displays the exact request locally and optionally saves it without contacting `/cluster`.

## 2. Problem

Process Documents can preview a package only after an in-memory prepared batch exists, while the dedicated main-menu Preview Request action remains a generic pending submenu. Named preview will provide reusable orchestration but not interactive collection or paging.

## 3. Solution Vision

Build a thin interactive adapter over shared preview services. It will collect inputs and clustering choices, prepare documents, show outcomes and the complete terminal-sized JSON preview, and offer an explicit Save Request JSON action while preserving the no-cluster guarantee.

## 4. Scope

- Interactive input paths, recursion choice, clustering settings, endpoint validation, preparation summary, and preview paging.
- Optional atomic request JSON save with suggested filename and overwrite confirmation.
- Replacement of only the Preview Request pending route.

## 5. Non-goals

- Calling `/cluster`, creating Excel/response artifacts, or changing Process Documents state.
- Reusing or mutating a separately retained prepared batch across main-menu workflows.

## 6. Technical Approach

- Reuse preparation, clustering resolution, `/list` validation, request creation, JSON paging, and `IJsonArtifactWriter` services.
- Keep menu orchestration separate from named `PreviewCommand`; both call the same application service.
- Make all file writing explicit and preserve the preview in memory after declined/failed saves.

## 7. Implementation Steps

1. Extract the named preview application operation behind an interactive-safe service contract.
2. Add the input/configuration collection flow and reuse existing path/preparation components.
3. Extend or adapt `PreparedJsonPackagePager` with an explicit Save Request JSON action.
4. Replace the generic Preview Request branch in `InteractiveMenu`.
5. Add tests for mixed preparation, exact JSON equality, no `/cluster`, paging, save/overwrite, failure retention, Escape, and cancellation.
6. Update README, preamble, CLI reference, privacy, preview, and troubleshooting documentation.

## 8. Acceptance Criteria

- Preview Request produces the same request as named preview for equivalent inputs/settings.
- The flow may call `/list` but never calls `/cluster`.
- Long JSON remains pageable and can be saved only through an explicit action.
- Save decline/failure leaves the preview available and never exposes a partial file.
- Every changed production method is covered; build, tests, formatting, publish, and whitespace verification pass.

## Completion Evidence

- Added a focused interactive preview flow and explicit pager save action while preserving the no-`/cluster` contract.
- The flow independently prepares its input, validates direct selections through `/list`, and retains its generated request after save decline or failure.

## 9. Risks and Assumptions

- This workflow intentionally repeats preparation rather than sharing mutable state with Process Documents.
- Complete preview JSON can contain sensitive extracted text and requires the same privacy warnings as processing.

## 10. Deferred Follow-up

- Any future cross-workflow prepared-batch sharing or remembered endpoint profiles.
