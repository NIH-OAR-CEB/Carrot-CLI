# Architecture and Deferred Acceptance Verification

Status: Pending

Category: Engineering assurance

Depends on: 05 through 09 operational and interactive feature plans

## 1. Outcome

Replace the remaining layout-only architecture and command-contract placeholders with active automated checks that enforce the final implemented-versus-deferred boundary.

## 2. Problem

`DocumentationConventionTests` contains two skipped layout stubs, and portions of `CommandContractTests` and `DocumentProcessingWorkflowTests` are reserved as future acceptances. Feature plans should activate their behavioral cases, but final source conventions, signature coverage, and absence of obsolete production layout stubs still need repository-wide enforcement.

## 3. Solution Vision

Use lightweight reflection and source scanning to protect the established internal visibility, XML documentation, separator headers, implementation regions, command metadata, and intentional stub policy. This final assurance plan will verify the completed surface rather than substitute broad scanners for feature tests.

## 4. Scope

- Activate the two architecture tests with deterministic repository-root discovery.
- Verify planned command names/options, visibility, documented public methods, and production DI resolution.
- Fail when obsolete `NotImplementedException("Layout stub only.")` bodies remain in completed production areas.
- Remove or rewrite stale scaffold-only XML exception documentation and fixture notes.

## 5. Non-goals

- Implementing product behavior, enforcing subjective style, or scanning generated/build output.
- Replacing unit, integration, command-harness, workbook, or HTTP tests.

## 6. Technical Approach

- Prefer Roslyn syntax parsing for C# conventions over fragile regular expressions where structural meaning matters.
- Limit scans to tracked production source and explicitly exclude generated, build, fixture, and Razor/template content.
- Maintain an explicit allowlist only for genuinely deferred seams; the intended end state after plans 05-09 is no operational production layout stub.

## 7. Implementation Steps

1. Implement robust repository-root and production-source discovery for tests.
2. Activate planned type/signature/internal-visibility reflection coverage.
3. Activate source convention checks for separator headers, XML summaries, implementation regions, private/public naming, and obsolete layout stubs.
4. Consolidate command metadata/DI assertions that remain deferred after feature plans.
5. Remove stale skip messages and update README/troubleshooting implementation-status text.

## 8. Acceptance Criteria

- Both architecture tests run and pass without environment-specific absolute paths.
- Completed production code contains no obsolete layout-stub throws or misleading layout-only XML documentation.
- The documented command/options surface and production DI graph are actively verified.
- Scanners ignore generated and non-C# content and produce actionable file/line failures.
- Full build, tests, formatting, publish, and whitespace verification pass with only the explicit local-service smoke test opt-in when unavailable.

## 9. Risks and Assumptions

- Overly broad scanners become brittle; checks must target stable repository conventions and report precise failures.
- Feature-specific skipped tests should be activated by their owning plans, not deferred wholesale to this plan.

## 10. Deferred Follow-up

- Add new acceptance checks only when later product capabilities introduce a durable repository-wide invariant.

