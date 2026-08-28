# CLI reference

The command surface is `process`, `preview`, `server-info`, `help`, and `about`; no arguments launch the interactive menu.

Before the main menu, the application displays welcome and getting-started content from the deployed `Content/application-preamble.md` file. The file is read on every launch, so its wording can be updated without rebuilding. If it is missing or unreadable, the application displays a short fallback directing the operator to `carrot-cli help getting-started` and continues to the menu.

The main menu provides Process Documents, Preview Request, Server Information, Help, About, and Exit. Each workflow submenu provides Execute, Help, and Back to Main Menu. Execute displays the exact status `Pending Implementation` and remains in the workflow submenu; no document, network, or output stub is invoked.

`carrot-cli help` displays Getting Started. `carrot-cli help <topic>` accepts case-insensitive canonical keys and aliases, including spaces or underscores normalized to hyphens. An unknown topic prints the canonical topic list and exits with code `1`. Help content is authored under `docs/help` and embedded in the executable assembly.

`process` accepts `--input`, `--endpoint`, `--output`, `--recursive`, `--algorithm`, `--language`, `--template`, `--parameters-file`, `--timeout-seconds`, `--overwrite`, `--no-json-artifacts`, `--quiet`, and `--log-file`. `preview` uses the discovery, extraction, and request options but does not call `/cluster`. `server-info` calls `/list` to display algorithms, languages, and templates.

When no template is selected, algorithm and language default to `Lingo` and `English`. Noninteractive endpoint precedence is `--endpoint`, then `CARROTCLI_ENDPOINT`; a missing endpoint is a configuration error. Noninteractive commands never prompt.

Help topics are getting started, process, preview, server information, commands/options, supported formats, extraction rules, clustering settings, output columns, Task Scheduler, exit codes, privacy, and troubleshooting.

Exit codes are: `0` success, `1` invalid command/configuration, `2` partial file-level success, `3` input/no processable documents, `4` endpoint or `/list` failure, `5` `/cluster` or response-contract failure, `6` report/artifact failure, and `130` cancellation.
