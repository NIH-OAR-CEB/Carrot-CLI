# iSearch

iSearch is an optional interactive workflow for checking the NIH iSearch service, discovering its live datasets, and submitting a bounded query. It is available from the main menu after credentials are configured.

## Configure User Secrets

From the repository or project directory, set the API key and a monitored contact address in User Secrets:

```powershell
dotnet user-secrets set "iSearch:apiKey" "YOUR_KEY"
dotnet user-secrets set "iSearch:contactEmail" "you@example.org"
```

The API key is sent only as the `apiKey` cookie to the iSearch HTTPS host. It is not placed in a URL, displayed, logged, or written to a file. The contact address is sent as the `From` header.

## Interactive workflow

1. Choose **iSearch** from the main menu.
2. The CLI validates both local settings and calls `GET /health`. The returned availability payload is displayed safely.
3. Dataset discovery runs only when the health payload reports `status: UP`. Dataset names come from the live `GET /datasets` response.
4. Select a returned database, choose **Submit Query**, and enter a nonempty free-text or Lucene query.
5. The CLI sends `POST /search` with the selected database, the query, `defaultOp: "AND"`, and at most 100 rows.

Results show returned and total counts plus generic JSON records. Queries and results remain in memory for the current visit only; no result files are written.

Use **Back to Main Menu** or Escape to leave a menu. Cancellation returns the standard exit code `130`.

## Troubleshooting

Missing credentials stop the workflow before any request is created. HTTP failures, redirects, malformed JSON, unavailable health status, empty datasets, and query failures are reported without exposing credentials or unbounded server response text. See [Troubleshooting](troubleshooting.md) for general CLI diagnostics.

## Command-line usage

iSearch is currently interactive-only:

```text
Program/script: C:\Tools\Carrot CLI\carrot-cli.exe
Arguments:
Start in: C:\Tools\Carrot CLI
```
