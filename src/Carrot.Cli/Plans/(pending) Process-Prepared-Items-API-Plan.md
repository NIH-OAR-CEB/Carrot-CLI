# Process Prepared Items API Integration

## Summary

Implement the interactive **Process Prepared Items** milestone using the prepared in-memory batch. Prompt for the Carrot service endpoint, validate the previewed Lingo/English configuration through `GET /list`, submit the exact previewed package through `POST /cluster`, correlate response indexes to the original ordered document array, and display paged matched results.

This milestone remains interactive and in-memory. Excel reports, JSON sidecars, logging artifacts, and named command execution remain deferred.

## Implementation Changes

### API and configuration

- Implement `EndpointResolver` for absolute HTTP/HTTPS service URLs ending in `/service`, accepting an optional trailing slash and rejecting credentials, query strings, and fragments. Prefill the prompt with `http://localhost:8080/service` without persisting it.
- Implement `CarrotApiClient` using host-managed `HttpClient`:
  - Call `/list` before submission and verify the previewed algorithm and language using exact identifiers.
  - POST the shared `ClusterRequestFactory` output to `/cluster` with `Content-Type` and `Accept` set to `application/json`.
  - Omit `template` and `indent` for this workflow; local views handle pretty-printing.
  - Disable redirects so document content cannot be forwarded to an unexpected location.
  - Parse documented 400/500 `CarrotErrorResponse` bodies and show type/message without exposing server stack traces in normal output.
  - Apply the configured two retries only to stateless transient failures such as transport errors, 408, 429, and 5xx responses. Never retry 400, malformed responses, redirects, or caller cancellation.
  - Treat the configured 120-second timeout as one overall operation budget across attempts.
- Register the API client through `IHttpClientFactory`, adding the matching `Microsoft.Extensions.Http` package if required.

### Correlation and processing model

- Add an internal `IPreparedDocumentProcessor.ProcessAsync(ProcessPreparedItemsRequest, CancellationToken)` boundary returning `OperationResult<ProcessedDocumentBatch>`.
- `ProcessedDocumentBatch` retains the endpoint, exact request, exact response, and one `ProcessedDocumentRow` per submitted document. Each row references its prepared source and all mapped `ClusterMembership` values.
- Implement `ClusterMembershipMapper` so:
  - `ClusterResponse.Clusters` is treated as the response array; an empty array is a valid success.
  - Every `clusters[].documents` value is interpreted as a zero-based index into the exact submitted `request.Documents` array, then correlated to `PreparedDocumentBatch.Documents[index]`.
  - Top-level and nested clusters are traversed depth-first in server order.
  - Multiple labels within a node use ` | `; nested path levels use ` > `.
  - Overlapping memberships are preserved, while duplicate references to the same document within one node produce one membership.
  - Every ready document receives a result row, including an explicit unassigned row when it appears in no cluster.
  - Any negative or out-of-range index invalidates the complete response; no partial mapping is displayed.
  - Scores are retained without rounding and documented as relative only within that response.
- Keep all ready documents in one request. Do not automatically split or merge calls because that changes clustering semantics.

### Interactive behavior and documentation

- Replace the pending Process action with endpoint collection, `/list` validation, submission, mapping, summary rendering, and automatic opening of a new processed-results pager.
- Display five documents per page with Carrot index, source path/title, assigned or unassigned status, membership count, category paths, and aligned scores.
- Put **Next Page** first and make it the default whenever available; support Previous Page and Escape/Back to Batch Actions.
- After success, add **View Processed Results** and retain the latest successful result. Reprocessing replaces it only after another successful response; failures leave the prepared batch and previous successful result intact.
- Update README, CLI reference, application preamble, Process/Privacy/Clustering/Troubleshooting help, and pending-status tests. Explain that complete extracted text is sent to the selected endpoint, memberships may overlap or be nested, empty cluster results are valid, and no files are written.
- Add nonblocking documentation guidance that Carrot2 generally works best with roughly 100–1,000 concise documents; do not enforce this as a runtime limit.
- Preserve the repository’s existing internal visibility, XML documentation, separator, and region conventions.

## Test Plan

- Add fake-handler API tests covering exact `/list` then `/cluster` ordering, request JSON equality with Preview JSON Package, content headers, URL construction, error-body translation, redirect rejection, retry limits, overall timeout, cancellation, and malformed JSON.
- Activate and expand mapper tests for the supplied `[0,1]` example, out-of-order indexes, overlapping clusters, multiple labels, nested paths, duplicate references, unassigned documents, empty clusters, and invalid indexes.
- Add processor tests proving response indexes correlate to prepared documents rather than source ordinals, including preparation batches containing failed rows and therefore gaps in source ordinals.
- Update Spectre `TestConsole` coverage for endpoint prompting, successful and failed processing, retained-batch behavior, automatic results display, paging defaults, Escape, retry/reprocess behavior, and zero ready items.
- Verify dependency injection, documentation, build, full test suite, formatting, and publish checks. Add an optional smoke test against the local Carrot2 4.8.6 service without making automated tests depend on a running server.

## Assumptions

- Processing submits the exact package shown by JSON Preview: configured `Lingo`, `English`, no template, and no parameter overrides.
- The bundled Carrot2 4.8.6 readmes, REST guides/reference, OpenAPI descriptor, templates, Java DCS examples, tuning pages, and release notes are authoritative. Where the older basics page mentions `text/json`, use `application/json` from the OpenAPI descriptor and bundled Java examples.
- Named `process`, `preview`, and `server-info` commands, algorithm/template selection UI, Excel output, request/response sidecars, and persistent endpoint profiles remain outside this milestone.
