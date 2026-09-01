# Server Information

The noninteractive `server-info` command calls the Carrot `/list` endpoint and displays available algorithms, languages, and templates.

In the interactive UI:

- **Execute** prompts for the service endpoint, calls `/list`, and displays the advertised algorithms, languages, and templates.
- **Help** displays this topic.
- **Back to Main Menu** leaves the workflow without side effects. The endpoint is not persisted.

The planned default service endpoint is `http://localhost:8080/service`.

## Command-line usage

The named `server-info` route is safe for unattended execution and never prompts. Endpoint precedence is the explicit `--endpoint` option followed by `CARROTCLI_ENDPOINT`. `--timeout-seconds` overrides the configured timeout. Algorithm and language identifiers are sorted ordinally for presentation, while template bodies are not displayed. The interactive flow uses the same `/list` contract with a nonpersisted `http://localhost:8080/service` default.

```text
Program/script: C:\Tools\Carrot CLI\carrot-cli.exe
Arguments: server-info --endpoint "http://localhost:8080/service"
Start in: C:\Tools\Carrot CLI
```

Exit codes: `0` for success, `1` for invalid configuration, `4` for endpoint or `/list` failures, and `130` for cancellation.
