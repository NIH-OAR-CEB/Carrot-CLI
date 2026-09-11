# Windows Task Scheduler

Publish with the `WindowsTaskScheduler` profile to create a self-contained, untrimmed `win-x64` folder deployment.

Use a noninteractive command and quote every path:

```text
Program/script: C:\Tools\Carrot CLI\carrot-cli.exe
Arguments: preview --input "C:\Data\Documents" --endpoint "http://localhost:8080/service" --output "C:\Data\Preview"
Start in: C:\Tools\Carrot CLI
```

The `Start in` value is required so relative configuration resolves consistently. `preview` is noninteractive: it validates the requested selection through `/list`, writes one complete request JSON artifact, and never calls `/cluster`. The example writes `C:\Data\Preview\Documents.request.json`; existing artifacts require `--overwrite`. Configure the task to record the process exit code: `0` is complete preview output, `1` is invalid configuration or unavailable selection, `2` is usable output with file-level failures, `3` is no ready document/input failure, `4` is an endpoint or `/list` failure, `6` is artifact persistence failure, and `130` indicates cancellation. `server-info` remains noninteractive and writes its deterministic report to standard output. Named process orchestration remains deferred.

For scheduled iSearch work, use the prompt-free named command and configure User Secrets for
`iSearch:apiKey` and `iSearch:contactEmail` outside the repository:

```text
Program/script: C:\Tools\Carrot CLI\carrot-cli.exe
Arguments: isearch --database grants --result-dataset Grants --query "*:*" --filter-query "fy:2024" --rows 100 --max-results 300 --output "C:\Data\Results\grants.xlsx" --overwrite
Start in: C:\Tools\Carrot CLI
```

Repeat `--query-field` and `--filter-query` for ordered qf/fq values. Use `--updated-after` and
`--updated-before` for update-date bounds, and use `--all-results` instead of `--max-results` only
when a complete walk is intentional. The command does not offer `--sort`. Exit code `0` means the
requested operation completed, `2` means a useful bounded walk stopped at its cap, `7` means an
iSearch failure, `4`/`5` means a Carrot failure, `6` means workbook persistence failure, and `130`
means cancellation.
