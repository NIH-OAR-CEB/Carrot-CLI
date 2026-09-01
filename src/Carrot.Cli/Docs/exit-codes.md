# Exit Codes

- `0` — Complete success or a normal interactive exit.
- `1` — Invalid command or configuration.
- `2` — Partial success with one or more file-level failures.
- `3` — Input failure or no processable documents.
- `4` — Endpoint validation or `/list` failure.
- `5` — `/cluster` request or response-contract failure.
- `6` — Report or artifact persistence failure.
- `130` — Cooperative cancellation.

Interactive preparation and Process Prepared Items failures are displayed without ending the menu session; the prepared batch and any prior processing success remain available. Ctrl+C returns `130`. Named `server-info` maps invalid configuration to `1`, `/list` failures to `4`, and cancellation to `130`. Preview Request remains pending and returns to its owning menu without changing the final normal-exit code. Interactive Server Information returns to its owning menu after displaying results or safe diagnostics.

## Command-line usage

Display the exit-code reference:

```text
Program/script: C:\Tools\Carrot CLI\carrot-cli.exe
Arguments: help exit-codes
Start in: C:\Tools\Carrot CLI
```
