# Troubleshooting

- Missing endpoint: supply `--endpoint` or set `CARROTCLI_ENDPOINT`. Interactive endpoint values are never persisted.
- No processable documents: confirm the input is a folder or ZIP containing a supported extension and that limits in `appsettings.json` are not exceeded.
- Scanned PDF: OCR is not included; supply a PDF with a searchable text layer.
- HTTP failure: verify the Carrot 4.8.6 service is reachable and that the endpoint ends at `/service`; use `server-info` to inspect `/list` before clustering.
- Scheduled task starts but produces no output: set the `Start in` directory and an explicit `--log-file`, then inspect the task's last result code.
- Partial success (`2`): inspect failed workbook rows while retaining valid clustering results.
- Welcome text is missing: confirm `Content/application-preamble.md` exists beside the deployed application and is readable. The menu remains available and directs operators to `carrot-cli help getting-started` when the file cannot be loaded.

The interactive shell, Markdown help, About output, and generated command metadata are implemented. Selecting Execute from a workflow menu displays `Pending Implementation` without reaching an operational stub. A `NotImplementedException` with `Layout stub only.` from a named operational route means that processing seam remains intentionally deferred.
