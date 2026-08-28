# Exit Codes

- `0` — Complete success or a normal interactive exit.
- `1` — Invalid command or configuration.
- `2` — Partial success with one or more file-level failures.
- `3` — Input failure or no processable documents.
- `4` — Endpoint validation or `/list` failure.
- `5` — `/cluster` request or response-contract failure.
- `6` — Report or artifact persistence failure.
- `130` — Cooperative cancellation.

Pending interactive operations do not end the session; they display `Pending Implementation` and return to their workflow menu.
