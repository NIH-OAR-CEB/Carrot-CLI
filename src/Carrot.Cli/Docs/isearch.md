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
4. Select a returned database. After selection, choose **View Fields** to retrieve the live schema or choose **Submit Query** and enter a nonempty free-text or Lucene query.
5. **View Fields** calls the authenticated `GET /fields/{dataset}` route for the retained dataset. The terminal table is sorted by `name` and displays `name`, `displayName`, `fieldType`, `defaultQueryField`, `defaultResultField`, `multiValued`, and `searchOnly`. Field names, labels, types, and Boolean flags belong to the live iSearch dataset schema; the CLI does not hard-code a field catalog. Field discovery permits a bounded response of up to 1 MiB by default because a complete dataset schema can be larger than ordinary operation diagnostics. Large displays use **Next Page** and **Previous Page**, while **Back to iSearch** or Escape returns to the dataset menu.
6. **Submit Query** sends the dataset-scoped `GET /search/{dataset}` request with URL-encoded `q`, `defaultOp=AND`, and at most 100 rows. The dataset-scoped route is used because the live body-based POST route currently returns HTTP 500.

Results show returned and total counts plus generic JSON records. Returning from field discovery or results retains the selected dataset for the next action. Queries, field metadata, and results remain in memory for the current visit only; no result files are written.

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
