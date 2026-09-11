# iSearch

iSearch is available as both an interactive workflow and a prompt-free `isearch` command. Both paths discover live databases, select a configured return dataset, and submit the same authenticated dataset-scoped GET search contract.

## Configure User Secrets

From the repository or project directory, set the API key and a monitored contact address in User Secrets:

```powershell
dotnet user-secrets set "iSearch:apiKey" "YOUR_KEY"
dotnet user-secrets set "iSearch:contactEmail" "you@example.org"
```

The API key is sent only as the `apiKey` cookie to the iSearch HTTPS host. It is not placed in a URL, displayed, logged, or written to a file. The contact address is sent as the `From` header.

## Configure return datasets

Return datasets are configured in `src/Carrot.Cli/appsettings.json` under `iSearchReturnTypes.Results`. The shared `Cardinality` object defines the four report field names used for every configured return dataset. Each other child name is an operator-facing label and its `DefaultFields` array is sent to iSearch as the ordered record-field set. Add additional child objects beside `Grants` for additional return datasets:

```json
{
  "iSearchReturnTypes": {
    "Results": {
      "Cardinality": {
        "TotalResultsFieldName": "totalCount",
        "CurrentResultsFieldName": "returnedCount",
        "PageNumberFieldName": "pageNumber",
        "TotalPagesFieldName": "totalPages"
      },
      "Grants": {
        "DefaultFields": ["grantNumber", "title", "abstract"]
      },
      "Summary": {
        "DefaultFields": ["id", "title"]
      }
    }
  }
}
```

The `*FieldName` values label the common report; they are not added to `DefaultFields` and are not sent as iSearch `fl` fields. iSearch supplies `returnedCount`, `totalCount`, `cursor`, and `results` in its response envelope. Field names in `DefaultFields` are dataset-specific and must match the selected live database. The CLI preserves configured order and reports iSearch validation errors safely; it does not silently fall back to an unfiltered response.

## Interactive workflow

1. Choose **iSearch** from the main menu.
2. The CLI validates both local settings and calls `GET /health`. The returned availability payload is displayed safely.
3. Dataset discovery runs only when the health payload reports `status: UP`. Dataset names come from the live `GET /datasets` response.
4. Select a returned database. **Select Return Dataset** appears immediately beneath **Select Database** and lists the configured `iSearchReturnTypes` child names. Select one before submitting a query.
5. After the database is selected, choose **View Fields** to retrieve the live schema. **View Fields** calls the authenticated `GET /fields/{dataset}` route for the selected live database. The terminal table is sorted by `name` and displays `name`, `displayName`, `fieldType`, `defaultQueryField`, `defaultResultField`, `multiValued`, and `searchOnly`. Field names, labels, types, and Boolean flags belong to the live iSearch dataset schema; the CLI does not hard-code a field catalog. Field discovery permits a bounded response of up to 1 MiB by default because a complete dataset schema can be larger than ordinary operation diagnostics. Large displays use **Next Page** and **Previous Page**, while **Back to iSearch** or Escape returns to the dataset menu. After both selections, choose **Submit Query** and enter a nonempty free-text or Lucene query.
6. **Submit Query** sends the selected return dataset's fields as the comma-separated `fl` result-field parameter on the dataset-scoped `GET /search/{dataset}` request, together with URL-encoded `q`, `defaultOp=AND`, and at most 100 rows. The dataset-scoped route is used because the live body-based POST route currently returns HTTP 500.

### Advanced query construction

After selecting a live database and return dataset, choose **Build Advanced Query** to construct a
guided request from the live field metadata. The builder explains the distinction between `q` (the
base query), `qf` (fields used for unqualified terms), `fq` (field-qualified filters), configured
`fl` result fields, `defaultOp`, `rows`, and the service-level `updatedAfter` and
`updatedBefore` bounds. A blank base query becomes `*:*`, which is useful when the restrictions are
provided by filters alone.

The filter picker uses the selected field's live name and type. For example, a returned numeric
fiscal-year field can receive `2024`, while a returned category field can receive a phrase such as
`Research Project Grants`. Multiple filters can be added or removed. Date bounds must use
`yyyy-MM-dd`; field-specific date or range filters must follow the Zulia query syntax
documented by the service.

Before a request is sent, the CLI displays its logical JSON package with pretty-printing, for example:

```json
{
  "dataset": "grants",
  "q": "*:*",
  "fl": ["grantNumber", "title"],
  "rows": 100,
  "defaultOp": "AND",
  "fq": ["fy:2024", "fundingCategory:\"Research Project Grants\""],
  "updatedAfter": "2024-10-01",
  "updatedBefore": "2025-09-30"
}
```

Choose **Edit Query Choices** to revisit a setting and regenerate the package, **Confirm and Submit**
to run it, or **Cancel** to return to the database menu. This JSON is a review representation of the
documented iSearch search package. The current client still sends the confirmed values as an encoded
dataset-scoped GET request, and the existing result pager carries the same filters and date bounds
through cursor continuation. Query drafts are held in memory for the current visit only.

Results show the configured cardinality names with total results, results in the current response, the current result page, and total result pages, followed by generic JSON records containing the selected fields. For a nonempty initial response, the result page is `1` and total pages uses ceiling division by the requested row limit. Empty data reports page `0` of `0`, providing an unambiguous stop condition for a data walk.

The results menu distinguishes **Next Display Page** from service-data actions. Next Display Page moves through terminal-sized lines already held for the current response and makes no network request. **Fetch Next Result Page** uses the service cursor to retrieve exactly one next chunk for the same live database, query, configured return dataset, fields, and row limit. **Fetch All Pages**, immediately below it, sequentially fetches every remaining service page for that query and updates the live **Search Summary** after each accepted page. Its progress bar reports loaded records divided by `totalCount` (for example, a partial final page can show 90.9% while Data Page is 3 of 3), and its Data Page value advances as responses are committed. Both fetch actions disappear when the service result set is complete. Routine iSearch HTTP-client information diagnostics are suppressed during the interactive workflow so logging cannot insert lines into the live summary region; warnings and errors still surface through the normal failure handling. A failed or canceled walk retains already accepted pages and does not claim completion; the operator can retry or go back. **Save iSearch Results to Excel** writes every record walked in this session—page 1 followed by page 2 and later pages—without fetching another page. The workbook has one `Results` worksheet with `ResultPage`, `ResultOrdinal`, configured fields, and any additional returned object fields; duplicate records are preserved in service order, and scalar/array records use a `Value` column. Terminal display pages are separate from iSearch result pages because one record can occupy multiple terminal lines. The shared **Search Summary** repeats the live dataset and return dataset, labels service chunks as **Data Page**, labels terminal rendering as **Display Page**, reports current/loaded/total records, and states whether another data chunk can be fetched. All continuation requests remain sequential and use the documented authenticated one-second request interval; bounded transient retries honor `Retry-After`. Returning from field discovery or results retains both selections; changing the live database requires selecting a return dataset again. Queries, field metadata, and walked results remain in memory for the current visit only; Save is explicit, local, atomic, and creates no JSON sidecar or log.

The result view uses one live renderable region for its cardinality panel, records, and Search Summary. Fetch All Pages replaces that existing region after each accepted page, keeping the summary in place; its active progress and paging values are orange until loading finishes. **Back to iSearch** and Escape clear the transient results region before the dataset menu is redrawn, so the prior results actions are not left above the new context. Use **Back to Main Menu** or Escape to leave a menu. Cancellation returns the standard exit code `130`.

When one or more records are loaded, **Categorize iSearch Results** submits the retained records to the configured Carrot endpoint. The request contains exactly `nihApplId`, `title`, `abstract`, and `specificAims` for each record; missing, numeric, or null iSearch scalars are sent as Carrot-compatible strings, and original present values remain in the categorized result. It does not fetch additional iSearch pages. After a successful response, the categorized view shows assignment status, category paths, scores, and membership counts. **Save Categorized iSearch Results to Excel** writes the loaded records and their category memberships to a local atomic workbook, including the four source fields and category JSON. An unassigned record remains in the workbook with blank category cells. Endpoint values are prompted for the current operation and are not persisted.

## Troubleshooting

Missing credentials or invalid/empty return-dataset configuration stop the workflow before any request is created. HTTP failures, redirects, malformed JSON, unavailable health status, empty datasets, invalid fields, and query failures are reported without exposing credentials or unbounded server response text. See [Troubleshooting](troubleshooting.md) for general CLI diagnostics.

## Command-line usage

The named command never prompts. Supply the live service database and configured return dataset,
then use the advanced options that correspond to the interactive builder:

| Option | Meaning |
| --- | --- |
| `--query <TEXT>` | Base `q`; defaults to `*:*`. |
| `--query-field <FIELD>` | Repeat for ordered `qf` fields. |
| `--filter-query <EXPRESSION>` | Repeat for ordered `fq` expressions such as `fy:2024` or `fundingCategory:"Research Project Grants"`. |
| `--default-op <AND\|OR>` | `defaultOp`; defaults to `AND`. |
| `--rows <1-100>` | Service page size; defaults to `100`. |
| `--updated-after <YYYY-MM-DD>` / `--updated-before <YYYY-MM-DD>` | Service update-date bounds. |
| `--result-dataset <NAME>` | Configured return dataset; its `DefaultFields` become `fl`. |

`--query-field` and `--filter-query` preserve the order in which they appear. When either is used,
the command validates the referenced field names against live `GET /fields/{database}` metadata.
Filter values are complete iSearch/Zulia expressions; the command does not invent a second typed
filter language. There is intentionally no `--sort` option because that control is unsupported.

Use a bounded search for scheduled work:

```powershell
carrot-cli isearch --database grants --result-dataset Grants --query "*:*" `
  --filter-query "fy:2024" --filter-query 'fundingCategory:"Research Project Grants"' `
  --query-field title --rows 100 --max-results 300 `
  --output "C:\Results\grants-2024.xlsx" --overwrite
```

Use `--all-results` instead of `--max-results` when every record through iSearch's reported
`totalCount` is required. Add `--categorize --categorized-output <PATH> --endpoint <URI>` to send
the accepted records through the existing Carrot path and write a categorized workbook. Existing
output files require `--overwrite`; parent directories are not created. Exit code `2` identifies a
useful bounded partial walk, `7` identifies iSearch health/discovery/search/paging failure, `4`/`5`
identify Carrot failures, `6` identifies workbook failure, and `130` identifies cancellation.

The command can be used from Task Scheduler:

```text
Program/script: C:\Tools\Carrot CLI\carrot-cli.exe
Arguments: isearch --database grants --result-dataset Grants --query "title:pain" --max-results 100 --output "C:\Results\grants.xlsx" --overwrite
Start in: C:\Tools\Carrot CLI
```
