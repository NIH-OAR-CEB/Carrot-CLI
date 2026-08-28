# Troubleshooting

- **Interactive input unavailable:** run the executable in a terminal, or use `carrot-cli --help` for named command usage.
- **Unknown help topic:** run `carrot-cli help` or select Help from the main menu to view valid topics.
- **Quoted path is rejected:** use one path per Add Path action and matching surrounding single or double quotes.
- **No supported documents:** direct files must use a supported extension; folders and ZIPs must contain at least one supported document.
- **Partial preparation:** review failed rows and batch warnings. Ready rows remain eligible for the future Process action.
- **Process Prepared Items is pending:** preparation is complete, but Carrot submission and report output remain deferred.
- **Preview or Server Information says Pending Implementation:** those interactive routes intentionally remain deferred.
- **Missing endpoint:** future named commands will require `--endpoint` or `CARROTCLI_ENDPOINT`.
- **Scanned PDF:** OCR is not included; provide a searchable text layer.
- **Content appears in the terminal:** prepared rows intentionally show a short extracted-text preview, while Preview JSON Package intentionally shows complete ready-document content; see Privacy before handling sensitive content.
- **JSON preview looks truncated:** `↪` marks the visual continuation of the same long JSON string. Use Next Page to continue; the serialized value is not truncated.
- **Scheduled task has no output:** configure **Start in**, quote paths, and provide `--log-file` when operational commands are implemented.
