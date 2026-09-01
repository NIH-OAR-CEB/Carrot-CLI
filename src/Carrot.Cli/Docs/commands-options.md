# Commands and Options

The command surface is:

```text
carrot-cli
carrot-cli process [options]
carrot-cli preview [options]
carrot-cli server-info [options]
carrot-cli help [topic]
carrot-cli about
carrot-cli --version
```

## Process

Named process accepts `--input`, `--endpoint`, `--output`, `--recursive`, `--algorithm`, `--language`, `--template`, `--parameters-file`, `--timeout-seconds`, `--overwrite`, `--no-json-artifacts`, `--quiet`, and `--log-file`. It discovers and extracts input, validates the exact selection through `/list`, sends all ready documents in one `/cluster` request, and writes a correlated `Results` workbook. Unless `--no-json-artifacts` is supplied, it also writes exact request and response JSON sidecars. `--quiet` suppresses normal progress and final summaries but never warnings or errors. `--log-file` must have an existing parent directory and appends safe UTF-8 lifecycle, warning/error, exit-code, and artifact-path records; it never records document content, API payloads, credentials, or server stack traces.

Without `--template`, omitted algorithm and language values default to exact `Lingo` and `English` identifiers. `--template` is mutually exclusive with both direct-selection options and delegates algorithm/language to the exact advertised server template. A `--parameters-file` may accompany either form and must be an existing `.json` file of at most 1,048,576 bytes containing valid UTF-8 JSON with one object root and unique property names. With a directory or omitted `--output`, the workbook name is `<input-name>-carrot-<run-id>.xlsx`; sidecars share that filename prefix. An explicit `--output` file becomes the workbook path and determines the sidecar prefix. Complete success returns `0`, usable output with file failures returns `2`, no ready documents returns `3`, `/list` failures return `4`, `/cluster` or response-mapping failures return `5`, output failures return `6`, and cancellation returns `130`.

For interactive preparation, run `carrot-cli` without arguments and select **Process Documents**. The interactive workflow accepts multiple paths and matching surrounding quotes; one Add Path action represents one file, folder, or ZIP.

## Preview

Implemented named preview accepts `--input`, `--endpoint`, `--output`, `--recursive`, `--algorithm`, `--language`, `--template`, `--parameters-file`, `--timeout-seconds`, and `--overwrite`. It discovers and extracts input, validates the exact clustering selection through `/list`, and atomically writes the complete request JSON without calling `/cluster` or prompting. Without `--output`, it writes `<input-parent>\<input-name>.request.json`; an existing output directory receives that deterministic filename, while an explicit output file is used directly. Existing artifacts require `--overwrite`. Complete output returns `0`; invalid configuration or unavailable selection returns `1`; partial file-level extraction output returns `2`; no ready documents returns `3`; `/list` failure returns `4`; artifact persistence failure returns `6`; and cancellation returns `130`.

## Server Information

Server information accepts `--endpoint` and `--timeout-seconds`, calls only `/list`, and displays sorted algorithm, language, and template identifiers. It does not display template bodies or write files. The named command never prompts; the interactive menu prompts for a nonpersisted endpoint and uses `http://localhost:8080/service` as its default. The named endpoint option takes precedence over `CARROTCLI_ENDPOINT`; its exit codes are `0` for success, `1` for invalid configuration, `4` for endpoint or list failures, and `130` for cancellation.

Named operational commands never prompt. Server information, named preview, and named process execution are active. Shared settings validation, clustering resolution, exact `/list` validation, Help, About, Version, interactive document processing, and interactive Server Information are active.

## Command-line usage

Display the complete command and option reference:

```text
Program/script: C:\Tools\Carrot CLI\carrot-cli.exe
Arguments: help commands-options
Start in: C:\Tools\Carrot CLI
```
