# Troubleshooting

- **Interactive input unavailable:** run the executable in a terminal, or use `carrot-cli --help` for named command usage.
- **Unknown help topic:** run `carrot-cli help` or select Help from the main menu to view valid topics.
- **Pending Implementation:** the selected UI route is available, but its operational workflow intentionally remains a stub.
- **Missing endpoint:** future named commands will require `--endpoint` or `CARROTCLI_ENDPOINT`.
- **Scanned PDF:** OCR is not included; provide a searchable text layer.
- **Scheduled task has no output:** configure **Start in**, quote paths, and provide `--log-file` when operational commands are implemented.
