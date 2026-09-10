# Privacy

Interactive preparation reads complete document content into application memory. Process Prepared Items sends that content to the endpoint you select. Confirm that the local workstation, visible terminal, network path, and Carrot service are approved for the source material.

- Prepared-result tables display up to 120 whitespace-normalized characters from each successful document.
- Preview JSON Package displays every ready document's full extracted title and content through bounded pages. Viewed pages may remain in terminal scrollback even though no JSON file is written.
- Paths, file metadata, extracted-character counts, warnings, and failures are displayed in the terminal and may remain in terminal scrollback.
- Starting over, returning to Main, or exiting releases the in-memory prepared batch. ZIP temporary files are removed immediately after extraction.
- Interactive endpoint values are not persisted.
- Process Prepared Items sends every ready document's full extracted title and text to the selected endpoint after `/list` validation. Redirects are rejected.
- Processed memberships and the latest successful request/response remain in memory until the batch is discarded or the application exits.
- Interactive processing writes no file automatically. **Save Processed Results to Excel** is a separate explicit action.
- Excel export includes source paths, filenames, hashes, endpoint/settings, memberships, and up to 30,000 characters of retained extracted content. Documents with several memberships repeat that information on one row per category; unassigned documents retain one blank-category row. Export excludes failed preparation rows and does not write request/response JSON sidecars or logs.
- iSearch retains only result pages explicitly fetched during the current query visit. **Fetch All Pages** is an explicit sequential action that can fetch every remaining page for the current query; **Save iSearch Results to Excel** combines the pages currently retained in service order, includes page/record provenance and returned fields, preserves duplicate records, and does not fetch additional pages. Treat the workbook and in-memory walk as sensitive returned research data; credentials are never written and no sidecar or log is created.
- Named `preview` request artifacts contain every ready document's complete extracted title and content after `/list` validation. Treat the artifact location and its contents as sensitive. Preview never calls `/cluster`. Named `process` writes the same request sidecar plus a response sidecar by default, alongside its workbook; use `--no-json-artifacts` when those JSON files should not be persisted.
- The implemented named-command log boundary records only UTC lifecycle events, coded warnings/errors, terminal status, exit code, and artifact paths. It never records extracted document text, request/response payloads, credentials, or server stack traces.
- A `--parameters-file` is read into memory before preview `/list` validation or future `/cluster` submission, and every property is included in the request. Treat parameter values and the file path as sensitive when they contain private labels, prompts, dictionaries, or service configuration. The parser enforces a 1,048,576-byte limit and retains no open file handle after resolution.

Protect terminal history and selected Excel or future JSON output folders according to the sensitivity of the source documents.

## Command-line usage

Review privacy guidance before submitting sensitive documents:

```text
Program/script: C:\Tools\Carrot CLI\carrot-cli.exe
Arguments: help privacy
Start in: C:\Tools\Carrot CLI
```
