# Windows Task Scheduler

## Command-line usage

Task Scheduler must use a named, noninteractive command. Quote all paths and configure the executable folder as **Start in**.

```text
Program/script: C:\Tools\Carrot CLI\carrot-cli.exe
Arguments: process --input "C:\Data\Documents" --endpoint "http://localhost:8080/service"
Start in: C:\Tools\Carrot CLI
```

Record the process exit code and provide an explicit log path for unattended diagnostics. Process writes an atomically replaced `Results` workbook and, unless `--no-json-artifacts` is supplied, matching request/response JSON sidecars. `--log-file` must name a file beneath an existing writable directory; records append as UTF-8 text and do not rotate automatically. `--quiet` suppresses normal console progress and summaries but never warnings or errors, and it does not suppress the requested file log. Do not schedule no-argument interactive execution.
