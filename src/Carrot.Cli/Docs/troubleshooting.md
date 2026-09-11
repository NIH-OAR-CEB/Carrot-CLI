# Troubleshooting

- **Interactive input unavailable:** run the executable in a terminal, or use `carrot-cli --help` for named command usage.
- **Unknown help topic:** run `carrot-cli help` or select Help from the main menu to view valid topics.
- **Help text is longer than the screen:** use **Next Page** and **Previous Page** to navigate, then select **Close Help** or press **Escape** to return.
- **Quoted path is rejected:** use one path per Add Path action and matching surrounding single or double quotes.
- **No supported documents:** direct files must use a supported extension; folders and ZIPs must contain at least one supported document.
- **Partial preparation:** review failed rows and batch warnings. Ready rows remain eligible for Process Prepared Items.
- **Endpoint rejected:** use an absolute HTTP or HTTPS URL ending exactly in `/service`, without credentials, a query string, or a fragment.
- **Algorithm, language, or template unavailable:** copy identifiers exactly from `/list`; matching is case-sensitive. A direct algorithm/language pair must be advertised together. Do not combine a template with either direct-selection option.
- **Parameters file rejected:** use an existing `.json` file no larger than 1,048,576 bytes containing valid UTF-8 JSON with one object root and no duplicate property names. Invalid, unreadable, oversized, or non-object files fail before `/cluster`.
- **HTTP processing failure:** confirm the service is reachable. HTTP 400, redirects, malformed JSON, and caller cancellation are not retried; transport errors, 408, 429, and server failures receive at most two retries within the overall operation timeout.
- **Every result is unassigned:** an empty cluster array is valid. Review the document set and content concision; Carrot2 generally works best with roughly 100–1,000 concise documents.
- **Response index failure:** Carrot returned a document index outside the submitted array, so the complete response was rejected and no partial mapping was displayed.
- **Excel path rejected:** press Enter to accept the complete timestamped suggestion, or edit it to a `.xlsx` filename under an existing directory. Relative paths and matching surrounding quotes are accepted.
- **Excel export failed:** close the workbook if another application has locked it, verify write permission, or choose another destination. The processed result remains available and no partial workbook replaces the destination.
- **JSON artifact persistence failed:** verify the destination parent exists and is writable, and enable overwrite only when replacement is intended. Failure or cancellation preserves an existing destination and removes temporary output. Named `preview` writes one request artifact after `/list` validation and never calls `/cluster`; named `process` writes request and response sidecars unless `--no-json-artifacts` is supplied.
- **Interactive preview validation failed:** correct the source path, endpoint ending in `/service`, or exact advertised algorithm/language identifiers, then retry. Preview Request may call `/list` but never calls `/cluster`.
- **Missing endpoint:** named `preview` and `server-info` require `--endpoint` or `CARROTCLI_ENDPOINT`.
- **Scanned PDF:** OCR is not included; provide a searchable text layer.
- **Content appears in the terminal or leaves the workstation:** prepared rows show a short preview, Preview JSON Package shows complete ready-document content, and Process Prepared Items sends complete extracted text to the selected endpoint; see Privacy before handling sensitive content.
- **JSON preview looks truncated:** `↪` marks the visual continuation of the same long JSON string. Use Next Page to continue; the serialized value is not truncated.
- **Scheduled task has no output:** configure **Start in**, quote paths, and choose an existing writable output directory or file path. Named `preview` writes its artifact; named `process` writes its workbook and optional sidecars. Both report stable exit codes, and `process --log-file` records safe lifecycle diagnostics.
- **iSearch cannot start:** set both `iSearch:apiKey` and `iSearch:contactEmail` with `dotnet user-secrets`; the workflow checks these values before any request. A negative `/health` status prevents dataset discovery. See [iSearch](isearch.md).
- **iSearch all-pages walk stops early:** the walk retains successful pages and reports the service, cursor, response, or throttle failure. Retry **Fetch All Pages** to continue from the last accepted page; an empty page or unchanged cursor before `totalCount` is reached is reported as incomplete rather than treated as 100% loaded.
- **Named iSearch option rejected:** use one `--database` returned by live `/datasets` and one configured `--result-dataset`. Repeat `--query-field` and `--filter-query` for qf/fq values; field references must exist in live `/fields/{database}` metadata. Dates must use `yyyy-MM-dd`, `--rows` must be 1 through 100, and `--all-results` cannot be combined with `--max-results`. The named command has no `--sort` option.
- **Named iSearch returns partial success:** exit code `2` means the requested bounded record cap was reached while more service pages remained. Use a larger `--max-results` or explicitly use `--all-results` when the retention ceiling permits a complete walk.

## Command-line usage

Display troubleshooting guidance without opening the interactive menu:

```text
Program/script: C:\Tools\Carrot CLI\carrot-cli.exe
Arguments: help troubleshooting
Start in: C:\Tools\Carrot CLI
```
