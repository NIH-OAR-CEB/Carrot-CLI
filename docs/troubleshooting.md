# Troubleshooting

- Missing endpoint: supply `--endpoint` or set `CARROTCLI_ENDPOINT`. Interactive endpoint values are never persisted.
- No processable documents: confirm the input is a folder or ZIP containing a supported extension and that limits in `appsettings.json` are not exceeded.
- Scanned PDF: OCR is not included; supply a PDF with a searchable text layer.
- HTTP failure: verify the Carrot 4.8.6 service is reachable and that the endpoint ends at `/service`; use `server-info` to inspect `/list` before clustering.
- Scheduled task starts but produces no output: set the `Start in` directory and an explicit `--log-file`, then inspect the task's last result code.
- Partial success (`2`): inspect failed workbook rows while retaining valid clustering results.

This scaffold is intentionally non-operational. A `NotImplementedException` with `Layout stub only.` means a future implementation seam was reached as designed.
