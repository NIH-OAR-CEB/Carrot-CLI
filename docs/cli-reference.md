# CLI reference

The planned command surface is `process`, `preview`, `server-info`, `help`, and `about`; no arguments launch the interactive menu.

`process` accepts `--input`, `--endpoint`, `--output`, `--recursive`, `--algorithm`, `--language`, `--template`, `--parameters-file`, `--timeout-seconds`, `--overwrite`, `--no-json-artifacts`, `--quiet`, and `--log-file`. `preview` uses the discovery, extraction, and request options but does not call `/cluster`. `server-info` calls `/list` to display algorithms, languages, and templates.

When no template is selected, algorithm and language default to `Lingo` and `English`. Noninteractive endpoint precedence is `--endpoint`, then `CARROTCLI_ENDPOINT`; a missing endpoint is a configuration error. Noninteractive commands never prompt.

Help topics are getting started, commands/options, supported formats, extraction rules, clustering settings, output columns, Task Scheduler, exit codes, privacy, and troubleshooting.

Exit codes are: `0` success, `1` invalid command/configuration, `2` partial file-level success, `3` input/no processable documents, `4` endpoint or `/list` failure, `5` `/cluster` or response-contract failure, `6` report/artifact failure, and `130` cancellation.
