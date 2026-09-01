# Windows Task Scheduler

Task Scheduler must use a named, noninteractive command. Quote all paths and configure the executable folder as **Start in**. Do not schedule no-argument interactive execution.

## Command-line usage

## Scenario: inspect server capability every morning

Use this when an operator needs a scheduled record of available algorithms, languages, and templates. The command calls only `/list`; it creates no output files. Configure Task Scheduler's output capture or redirect standard output if a retained report is required.

```text
Program/script: C:\Tools\Carrot CLI\carrot-cli.exe
Arguments: server-info --endpoint "http://localhost:8080/service"
Start in: C:\Tools\Carrot CLI
```

## Scenario: validate and retain a request without clustering

Use this for approval workflows where the request body must be reviewed or passed to another API tool. Preview validates `/list` but never calls `/cluster`. The explicit file is one indented UTF-8 JSON API request at `C:\Results\nightly.request.json`; no workbook or response sidecar is created.

```text
Program/script: C:\Tools\Carrot CLI\carrot-cli.exe
Arguments: preview --input "C:\Data\Nightly.zip" --recursive --endpoint "http://localhost:8080/service" --output "C:\Results\nightly.request.json" --overwrite
Start in: C:\Tools\Carrot CLI
```

## Scenario: cluster a nightly folder and import results into Excel

Use this for unattended end-to-end processing. With `--output "C:\Results"`, process writes three atomically replaced files in that existing directory: `<input-name>-carrot-<run-id>.xlsx` (one `Results` worksheet), plus same-prefix `.request.json` and `.response.json` sidecars. The workbook contains one row per category membership; unassigned documents retain one blank-category row.

```text
Program/script: C:\Tools\Carrot CLI\carrot-cli.exe
Arguments: process --input "C:\Data\Documents" --recursive --endpoint "http://localhost:8080/service" --output "C:\Results" --log-file "C:\Logs\carrot-nightly.log" --quiet
Start in: C:\Tools\Carrot CLI
```

## Scenario: write only an Excel workbook

Use this when request and response payloads must not be persisted. The explicit `--output` file becomes the atomic `.xlsx` workbook destination. `--no-json-artifacts` suppresses sidecars; the log remains UTF-8 text appended at the specified location.

```text
Program/script: C:\Tools\Carrot CLI\carrot-cli.exe
Arguments: process --input "C:\Data\Documents" --endpoint "http://localhost:8080/service" --output "C:\Results\weekly-results.xlsx" --no-json-artifacts --log-file "C:\Logs\carrot-weekly.log"
Start in: C:\Tools\Carrot CLI
```

Record the exit code. `0` is complete success, `2` means usable output with file-level failures, `3` input failure, `4` endpoint or `/list` failure, `5` cluster/response failure, `6` output failure, and `130` cancellation. `--log-file` must name a file beneath an existing writable directory; records append as UTF-8 text and do not rotate automatically. `--quiet` suppresses normal console progress and summaries but never warnings or errors, and it does not suppress the requested file log.
