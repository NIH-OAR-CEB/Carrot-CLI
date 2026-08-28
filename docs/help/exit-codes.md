# Exit Codes

- `0` — Complete success or a normal interactive exit.
- `1` — Invalid command or configuration.
- `2` — Partial success with one or more file-level failures.
- `3` — Input failure or no processable documents.
- `4` — Endpoint validation or `/list` failure.
- `5` — `/cluster` request or response-contract failure.
- `6` — Report or artifact persistence failure.
- `130` — Cooperative cancellation.

Interactive preparation failures are displayed as rows and messages without ending the menu session. Ctrl+C returns `130`. The pending Process Prepared Items action, Preview Request, and Server Information return to their owning menus without changing the final normal-exit code.
