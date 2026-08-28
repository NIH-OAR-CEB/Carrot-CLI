# CLI reference

The command surface is `process`, `preview`, `server-info`, `help`, and `about`; no arguments launch the interactive menu.

Before the main menu, the application displays welcome and getting-started content from the deployed `Content/application-preamble.md` file. The file is read on every launch, so its wording can be updated without rebuilding. If it is missing or unreadable, the application displays a short fallback directing the operator to `carrot-cli help getting-started` and continues to the menu.

The main menu provides Process Documents, Preview Request, Server Information, Help, About, and Exit. Preview Request and Server Information retain Execute, Help, and Back to Main Menu; their Execute actions display `Pending Implementation`.

Process Documents provides an editable ordered input list. Add Path accepts one individual supported file, folder, or ZIP archive and removes matching surrounding single or double quotes. Paths are normalized to absolute paths, so both quoted paths with spaces and relative paths are supported. Wildcards, environment-variable expansion, and multiple pasted paths in one prompt are not expanded.

Prepare Documents optionally enables recursion for every folder, discovers and deduplicates sources, safely expands ZIPs, extracts searchable text, hashes successful documents, and retains failures for review. Inputs follow entered order; documents within a folder or archive follow normalized relative-path order. Duplicate canonical sources keep their first occurrence.

Prepared results display five rows per page with status, source, type, size, extracted character count, a 120-character normalized content preview, and failure text. Next/Previous navigate pages; Next is the default whenever another page exists, and Escape returns to retained Batch Actions. Preview JSON Package serializes the exact indented `/cluster` body using the configured default language and algorithm and every ready document's complete title/content. Local source metadata is excluded, and the preview neither contacts Carrot nor writes a file. Process Prepared Items reports the ready count but does not yet call `/cluster` or create output. Start Over and Back require confirmation before discarding the in-memory batch.

`carrot-cli help` displays Getting Started. `carrot-cli help <topic>` accepts case-insensitive canonical keys and aliases, including spaces or underscores normalized to hyphens. An unknown topic prints the canonical topic list and exits with code `1`. Help content is authored under `docs/help` and embedded in the executable assembly.

The planned named `process` command accepts `--input`, `--endpoint`, `--output`, `--recursive`, `--algorithm`, `--language`, `--template`, `--parameters-file`, `--timeout-seconds`, `--overwrite`, `--no-json-artifacts`, `--quiet`, and `--log-file`. `preview` uses the discovery, extraction, and request options but does not call `/cluster`. `server-info` calls `/list` to display algorithms, languages, and templates. These named operational routes remain deferred and never prompt.

When no template is selected, algorithm and language default to `Lingo` and `English`. Noninteractive endpoint precedence is `--endpoint`, then `CARROTCLI_ENDPOINT`; a missing endpoint is a configuration error. Noninteractive commands never prompt.

Help topics are getting started, process, preview, server information, commands/options, supported formats, extraction rules, clustering settings, output columns, Task Scheduler, exit codes, privacy, and troubleshooting.

Exit codes are: `0` success, `1` invalid command/configuration, `2` partial file-level success, `3` input/no processable documents, `4` endpoint or `/list` failure, `5` `/cluster` or response-contract failure, `6` report/artifact failure, and `130` cancellation.
