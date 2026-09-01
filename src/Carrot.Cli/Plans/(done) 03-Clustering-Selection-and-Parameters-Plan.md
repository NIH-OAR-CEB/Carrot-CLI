# Clustering Selection and Parameters

Status: Complete

Category: Clustering configuration

Depends on: 01 Named Command Foundation

## 1. Outcome

Provide one validated clustering-configuration model that supports algorithm, language, template, and JSON parameter overrides while preserving the current interactive `Lingo`/`English` defaults.

## 2. Problem

The request contract and command options already model algorithm, language, template, and parameters, but `ClusterRequestFactory` always emits configured defaults and `PreparedDocumentProcessor` validates only the fixed algorithm/language pair. Parameter-file parsing and template validation are absent.

## 3. Solution Vision

Introduce a focused resolver for clustering selections, validate those selections against `/list`, and make `ClusterRequestFactory` consume an explicit resolved configuration. Named and future interactive callers will share this path; the existing interactive workflow will pass the current defaults until its own selection UI is implemented.

## 4. Scope

- Resolve algorithm/language/template semantics and parse a parameter file whose root is a JSON object.
- Preserve arbitrary nested JSON parameter values as `JsonElement` data.
- Validate exact identifiers and compatible algorithm/language combinations through `ListResponse`.
- Refactor request creation and processing to accept a resolved configuration while preserving one global document request.

## 5. Non-goals

- Building interactive selection prompts.
- Implementing named command orchestration or file logging.
- Inventing client-side schemas for algorithm-specific parameter keys.

## 6. Technical Approach

- Represent resolved selections with an immutable internal model rather than passing unrelated nullable strings.
- Treat a template as the `/cluster?template=` query selection; omit algorithm/language only when template semantics require the server to supply them.
- Require parameter files to exist, be valid UTF-8 JSON objects, and remain within a documented size limit.
- Keep validation case-sensitive because Carrot advertises exact identifiers.

## 7. Implementation Steps

1. Add the resolved configuration model and resolver with structured validation failures.
2. Extend `ClusterRequestFactory` to create requests from explicit resolved selections and parameters.
3. Extract `/list` selection validation from `PreparedDocumentProcessor` into a reusable service used by later commands.
4. Preserve the current interactive default path and update its tests for behavioral equivalence.
5. Add parameter parsing, template, algorithm/language, invalid-combination, and serialization coverage.
6. Update clustering, commands/options, privacy, and troubleshooting help.

## 8. Acceptance Criteria

- Defaults still produce the current `Lingo`/`English` request with no template or parameters.
- Explicit algorithm/language and template selections are validated against exact `/list` data.
- Valid nested parameter JSON reaches `ClusterRequest.Parameters` without type loss.
- Invalid, non-object, oversized, or unreadable parameter files fail before `/cluster`.
- All changed production methods are covered; build, tests, formatting, and whitespace verification pass.

## 9. Risks and Assumptions

- Carrot template metadata is authoritative for template names, but may not expose every effective parameter.
- Template and explicit algorithm/language precedence must be documented clearly to prevent ambiguous requests.

## 10. Deferred Follow-up

- Interactive clustering-selection prompts and named preview/process execution.

## 11. Completion Evidence

- Added immutable `ClusteringSelection` and `ClusteringConfiguration` models plus a reusable resolver that applies defaults, rejects ambiguous template/direct combinations, and parses detached JSON parameter values.
- Parameter files require an existing `.json` path, strict valid UTF-8, a unique-property object root, and no more than the configured 1,048,576-byte default; missing, malformed, non-object, duplicate, oversized, locked, and canceled reads have explicit behavior.
- Added an exact ordinal `/list` validator for algorithm/language compatibility and template names, independent of caller dictionary comparers.
- `ClusterRequestFactory` consumes explicit resolved configuration, and `PreparedDocumentProcessor` resolves parameters before `/list`, validates selections before `/cluster`, passes templates through the query selection, and preserves one global request.
- Named process/preview request settings now carry one normalized clustering selection. Interactive processing still emits `Lingo`/`English` with no template or parameters.
- README, CLI reference, troubleshooting, and embedded clustering, command, privacy, and troubleshooting help document precedence, the size and JSON contract, and deferred command/UI scope.
- Verification completed with 205 passing tests, 6 intentionally deferred tests, and 1 opt-in local Carrot smoke test not selected; the Release build completed with 0 warnings and 0 errors, and formatting and whitespace checks passed.
