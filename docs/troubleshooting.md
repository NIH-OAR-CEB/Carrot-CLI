# Troubleshooting

- Missing endpoint: supply `--endpoint` or set `CARROTCLI_ENDPOINT`. Interactive endpoint values are never persisted.
- Path rejected: enter one existing file, folder, or ZIP per Add Path action. Matching single or double quotes are accepted.
- No processable documents: confirm direct files use a supported extension, folders/ZIPs contain supported documents, and `appsettings.json` limits are not exceeded.
- Partial preparation: review warning messages and failed rows. Ready rows remain available from Batch Actions.
- Scanned PDF: OCR is not included; supply a PDF with a searchable text layer.
- HTTP failure: verify the Carrot 4.8.6 service is reachable and that the endpoint ends at `/service`; use `server-info` to inspect `/list` before clustering.
- Scheduled task starts but produces no output: set the `Start in` directory and an explicit `--log-file`, then inspect the task's last result code.
- Partial success (`2`): inspect failed workbook rows while retaining valid clustering results.
- Welcome text is missing: confirm `Content/application-preamble.md` exists beside the deployed application and is readable. The menu remains available and directs operators to `carrot-cli help getting-started` when the file cannot be loaded.

The interactive shell, document preparation, paged review, local JSON package preview, Markdown help, About output, and generated command metadata are implemented. Preview JSON Package displays the complete request locally without contacting Carrot or writing an artifact. Process Prepared Items reports its ready count without contacting Carrot or writing artifacts. Preview Request, Server Information, and named operational routes remain deferred; a `NotImplementedException` with `Layout stub only.` from a named operational route identifies one of those later seams.
