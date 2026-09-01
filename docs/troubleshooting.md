# Troubleshooting

- Missing endpoint: supply `--endpoint` or set `CARROTCLI_ENDPOINT`. Interactive endpoint values are never persisted.
- Path rejected: enter one existing file, folder, or ZIP per Add Path action. Matching single or double quotes are accepted.
- No processable documents: confirm direct files use a supported extension, folders/ZIPs contain supported documents, and `appsettings.json` limits are not exceeded.
- Partial preparation: review warning messages and failed rows. Ready rows remain available from Batch Actions.
- Interactive endpoint rejected: use an absolute HTTP or HTTPS URL ending exactly in `/service`, without credentials, query text, or a fragment.
- Scanned PDF: OCR is not included; supply a PDF with a searchable text layer.
- HTTP failure: verify the Carrot 4.8.6 service is reachable and that the endpoint ends at `/service`; use `server-info` to inspect `/list` before clustering.
- Clustering selection rejected: copy algorithm, language, or template identifiers exactly from `/list`. Do not combine `--template` with `--algorithm` or `--language`; template selection delegates both fields to the server.
- Parameters file rejected: use an existing `.json` file of at most 1,048,576 bytes containing valid UTF-8 JSON with one object root and no duplicate property names. Parameter validation completes before `/cluster`.
- Excel output path rejected: press Enter to accept the complete timestamped suggestion, or edit it to a filename ending in `.xlsx` under an existing directory. Matching surrounding quotes and relative paths are accepted.
- Excel export failed: close an existing workbook if it is locked, verify destination write permission, and retry or select another path. The retained processed result remains available and a failed save leaves no partial destination.
- JSON artifact persistence failed: verify the destination parent exists and is writable, then approve overwrite only when replacement is intended. Atomic persistence preserves an existing destination and removes its temporary sibling after failure or cancellation. Named commands do not invoke this implemented boundary until their later orchestration milestones.
- Scheduled task starts but produces no output: set the `Start in` directory and an explicit `--log-file` under an existing writable directory, then inspect the task's last result code. `--quiet` hides normal summaries but never warnings or errors; named command orchestration remains deferred.
- Partial success (`2`): inspect failed workbook rows while retaining valid clustering results.
- Welcome text is missing: confirm `Content/application-preamble.md` exists beside the deployed application and is readable. The menu remains available and directs operators to `carrot-cli help getting-started` when the file cannot be loaded.

The interactive shell, document preparation, paged review, paged and visually wrapped local JSON package preview, Process Prepared Items API integration, correlated result paging, explicit atomic Excel export, atomic JSON persistence boundary, Markdown help, About output, and generated command metadata are implemented. Preview JSON Package displays the complete request locally without contacting Carrot. Process Prepared Items sends that exact request and keeps request/response/results in memory; only the separate explicit Excel action currently writes a file. Preview Request, Server Information, named JSON artifact orchestration, and named operational routes remain deferred; a `NotImplementedException` with `Layout stub only.` from a named operational route identifies one of those later seams.
