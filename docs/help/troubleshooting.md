# Troubleshooting

- **Interactive input unavailable:** run the executable in a terminal, or use `carrot-cli --help` for named command usage.
- **Unknown help topic:** run `carrot-cli help` or select Help from the main menu to view valid topics.
- **Quoted path is rejected:** use one path per Add Path action and matching surrounding single or double quotes.
- **No supported documents:** direct files must use a supported extension; folders and ZIPs must contain at least one supported document.
- **Partial preparation:** review failed rows and batch warnings. Ready rows remain eligible for Process Prepared Items.
- **Endpoint rejected:** use an absolute HTTP or HTTPS URL ending exactly in `/service`, without credentials, a query string, or a fragment.
- **Algorithm or language unavailable:** confirm `/list` advertises exact `Lingo` and `English` identifiers together.
- **HTTP processing failure:** confirm the service is reachable. HTTP 400, redirects, malformed JSON, and caller cancellation are not retried; transport errors, 408, 429, and server failures receive at most two retries within the overall operation timeout.
- **Every result is unassigned:** an empty cluster array is valid. Review the document set and content concision; Carrot2 generally works best with roughly 100–1,000 concise documents.
- **Response index failure:** Carrot returned a document index outside the submitted array, so the complete response was rejected and no partial mapping was displayed.
- **Excel path rejected:** press Enter to accept the complete timestamped suggestion, or edit it to a `.xlsx` filename under an existing directory. Relative paths and matching surrounding quotes are accepted.
- **Excel export failed:** close the workbook if another application has locked it, verify write permission, or choose another destination. The processed result remains available and no partial workbook replaces the destination.
- **Preview or Server Information says Pending Implementation:** those interactive routes intentionally remain deferred.
- **Missing endpoint:** future named commands will require `--endpoint` or `CARROTCLI_ENDPOINT`.
- **Scanned PDF:** OCR is not included; provide a searchable text layer.
- **Content appears in the terminal or leaves the workstation:** prepared rows show a short preview, Preview JSON Package shows complete ready-document content, and Process Prepared Items sends complete extracted text to the selected endpoint; see Privacy before handling sensitive content.
- **JSON preview looks truncated:** `↪` marks the visual continuation of the same long JSON string. Use Next Page to continue; the serialized value is not truncated.
- **Scheduled task has no output:** configure **Start in**, quote paths, and provide `--log-file` when operational commands are implemented.
