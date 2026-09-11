# Exit Codes

- `0` — Complete success or a normal interactive exit.
- `1` — Invalid command or configuration.
- `2` — Partial success with one or more file-level failures.
- `3` — Input failure or no processable documents.
- `4` — Endpoint validation or `/list` failure.
- `5` — `/cluster` request or response-contract failure.
- `6` — Report or artifact persistence failure.
- `7` — Named iSearch health, discovery, search, or paging failure.
- `130` — Cooperative cancellation.

Interactive preparation, Preview Request, and Process Prepared Items failures are displayed without ending the menu session. Interactive Preview Request may call `/list`, never calls `/cluster`, and keeps its displayed request available after a declined or failed explicit save. Ctrl+C returns `130`. Named `server-info` maps invalid configuration to `1`, `/list` failures to `4`, and cancellation to `130`. Named `preview` maps complete output to `0`, usable output with file failures to `2`, no ready documents to `3`, configuration failures to `1`, `/list` failures to `4`, artifact failures to `6`, and cancellation to `130`; it never calls `/cluster`. Named `process` maps the same preparation and `/list` outcomes, maps `/cluster` or response-correlation failures to `5`, and maps workbook or sidecar persistence failures to `6`. Interactive Server Information returns to its owning menu after displaying results or safe diagnostics.

## Command-line usage

Display the exit-code reference:

```text
Program/script: C:\Tools\Carrot CLI\carrot-cli.exe
Arguments: help exit-codes
Start in: C:\Tools\Carrot CLI
```
