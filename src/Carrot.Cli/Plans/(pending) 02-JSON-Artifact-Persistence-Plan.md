# JSON Artifact Persistence

Status: Pending

Category: Artifacts and observability

Depends on: 01 Named Command Foundation

## 1. Outcome

Implement atomic, exact JSON persistence for request previews and optional process request/response sidecars through `IJsonArtifactWriter`.

## 2. Problem

`JsonArtifactWriter` still throws from its constructor and public `WriteAsync` method. The named command models promise preview output and request/response sidecars, but the only current JSON view is terminal-only and intentionally writes no files.

## 3. Solution Vision

Make `JsonArtifactWriter` a serialization boundary over the already implemented `AtomicFileWriter`. It will serialize complete contracts directly to a same-directory temporary file and expose the final artifact only after a successful flush and promotion.

## 4. Scope

- Exact UTF-8 JSON serialization for `ClusterRequest`, `ClusterResponse`, and compatible generic artifacts.
- Indented preview output and deterministic serializer settings where contract order is stable.
- Atomic new-file and overwrite behavior, cancellation, safe failure propagation, and DI registration.
- Direct tests for the public generic writer method and its constructor behavior.

## 5. Non-goals

- Deciding named-run artifact filenames or orchestration order.
- Writing Excel, logs, CSV, or interactive sidecars.
- Calling Carrot or changing API serialization.

## 6. Technical Approach

- Inject `AtomicFileWriter`; do not duplicate temporary-file promotion logic.
- Serialize asynchronously to the writer-owned stream with explicit UTF-8 settings and no content truncation.
- Preserve formula-like strings and arbitrary `JsonElement` value kinds exactly as JSON data.
- Propagate caller cancellation and translate only expected filesystem/serialization failures at the orchestration boundary.

## 7. Implementation Steps

1. Implement constructor storage and reusable serializer options in `JsonArtifactWriter`.
2. Implement `WriteAsync<TArtifact>` through `AtomicFileWriter.WriteAsync` with argument checks and cancellation.
3. Register `IJsonArtifactWriter` in `ServiceRegistration`.
4. Add tests that reopen and deserialize request/response artifacts, cover nested parameter values, overwrite decisions, cancellation, writer failure, and temporary-file cleanup.
5. Update output-format, privacy, and troubleshooting documentation to distinguish optional named-command artifacts from interactive Excel export.

## 8. Acceptance Criteria

- A successful write produces one complete, deserializable UTF-8 JSON file at the requested path.
- Existing destinations are preserved unless overwrite is explicitly allowed.
- Failure or cancellation exposes no partial destination and leaves no temporary sibling.
- Request and response values round-trip without lost fields or changed JSON value kinds.
- All changed production methods are covered; build, tests, formatting, and whitespace verification pass.

## 9. Risks and Assumptions

- Contract property order is currently stable but consumers must rely on JSON meaning, not byte-for-byte property order.
- Very large extracted content can create large sidecars; safeguard policy remains owned by preparation and named workflow plans.

## 10. Deferred Follow-up

- Named preview/process artifact naming and command orchestration.

