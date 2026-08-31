# Exit Codes

- `0` — Complete success or a normal interactive exit.
- `1` — Invalid command or configuration.
- `2` — Partial success with one or more file-level failures.
- `3` — Input failure or no processable documents.
- `4` — Endpoint validation or `/list` failure.
- `5` — `/cluster` request or response-contract failure.
- `6` — Report or artifact persistence failure.
- `130` — Cooperative cancellation.

Interactive preparation and Process Prepared Items failures are displayed without ending the menu session; the prepared batch and any prior processing success remain available. Ctrl+C returns `130`. Preview Request and Server Information remain pending and return to their owning menus without changing the final normal-exit code.

## Command-line usage

Display the exit-code reference:

```text
Program/script: C:\Tools\Carrot CLI\carrot-cli.exe
Arguments: help exit-codes
Start in: C:\Tools\Carrot CLI
```
