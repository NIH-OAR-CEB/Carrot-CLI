# Carrot CLI

Carrot CLI is a Windows command-line client for the [Carrot2 Document Clustering Server](https://carrot2.github.io/release/4.8.6/doc/). It prepares supported local documents, validates clustering settings with the server, submits a single clustering request, and presents or exports correlated results.

It targets .NET 10 and is compatible with the Carrot 4.8.6 server API. You need access to a running Carrot service endpoint, typically `http://localhost:8080/service`.

## What it does

- Provides an interactive menu for preparing, previewing, clustering, and exporting documents.
- Provides an optional interactive iSearch health, dataset discovery, and bounded query workflow.
- Offers script-friendly `server-info`, `preview`, and `process` commands that never prompt.
- Reads Word, Excel, PowerPoint, text, Markdown, searchable PDF, folders, and ZIP archives.
- Validates the selected algorithm, language, or server template through `GET /list` before processing.
- Sends all ready documents in one `POST /cluster` request, preserving clustering semantics.
- Writes atomic JSON and Excel artifacts for named commands; interactive processing keeps results in memory until an explicit Excel export.

## Quick start

Build and run the interactive application:

```powershell
dotnet restore .\Carrot-CLI.slnx
dotnet run --project .\src\Carrot.Cli\Carrot.Cli.csproj
```

Choose **Process Documents**, add one or more files, folders, or ZIP archives, prepare the batch, inspect the generated request, then process it. The interactive endpoint defaults to `http://localhost:8080/service` and is not persisted.

To inspect a server without creating files:

```powershell
dotnet run --project .\src\Carrot.Cli\Carrot.Cli.csproj -- server-info --endpoint "http://localhost:8080/service"
```

To enable **iSearch**, store `iSearch:apiKey` and `iSearch:contactEmail` in User Secrets. The interactive route checks `/health`, discovers databases live, lets you select a configured return dataset from `iSearchReturnTypes.Results`, sends its record fields as `fl`, reports shared total/current/result-page cardinality with the response cursor, distinguishes local **Next Display Page** navigation from one-request **Fetch Next Result Page** retrieval, and offers **Fetch All Pages** below it to sequentially walk every remaining page while the Search Summary shows actual loaded-record progress. **Save iSearch Results to Excel** includes all pages walked in the current query session. The key is sent only as a cookie, requests are limited to 100 records per page, and continuation respects the documented authenticated one-second interval. See [iSearch help](src/Carrot.Cli/Docs/isearch.md).

## Commands

```text
carrot-cli
carrot-cli process [options]
carrot-cli preview [options]
carrot-cli server-info [options]
carrot-cli help [topic]
carrot-cli about
carrot-cli --version
```

| Command | Purpose |
| --- | --- |
| No command | Opens the interactive menu. |
| `server-info` | Calls `/list` and displays sorted algorithms, languages, and templates. No files are written. |
| `preview` | Prepares documents, validates the selection through `/list`, and atomically writes the complete `/cluster` request JSON. It never calls `/cluster`. |
| `process` | Prepares and validates documents, sends one `/cluster` request, and atomically writes a `Results` workbook. Request and response JSON sidecars are also written unless disabled. |
| `help [topic]` | Shows embedded help, including `getting-started`, `commands-options`, `task-scheduler`, and `troubleshooting`. |

The executable accepts `--help` for command-specific usage. Named commands do not prompt, making them suitable for Task Scheduler and automation.

## Common examples

```powershell
# Discover available server capabilities.
carrot-cli server-info --endpoint "http://localhost:8080/service"

# Build and save a request without clustering.
carrot-cli preview `
  --input "C:\Data\Documents.zip" `
  --endpoint "http://localhost:8080/service" `
  --output "C:\Results\documents.request.json"

# Process a folder recursively and write an Excel workbook plus JSON sidecars.
carrot-cli process `
  --input "C:\Data\Documents" `
  --recursive `
  --endpoint "http://localhost:8080/service" `
  --output "C:\Results" `
  --log-file "C:\Logs\carrot-process.log"

# Use an advertised server template and write only the workbook.
carrot-cli process `
  --input "C:\Data\Documents" `
  --endpoint "http://localhost:8080/service" `
  --template frontend-default `
  --no-json-artifacts `
  --quiet
```

Use `--algorithm` and `--language` to select exact server-advertised identifiers; when neither is supplied, they default to `Lingo` and `English`. Alternatively, use `--template`, which cannot be combined with either direct-selection option. `--parameters-file` accepts a validated JSON object to accompany either selection method.

For named `process`, an omitted `--output` or an output directory creates `<input-name>-carrot-<run-id>.xlsx`; matching request and response sidecars use the same prefix. `preview` defaults to `<input-name>.request.json`. Existing artifacts require `--overwrite`.

## Supported input

| Type | Details |
| --- | --- |
| Word | `.docx` body and table text |
| Excel | `.xlsx` nonempty cells, sheet by sheet |
| PowerPoint | `.pptx` slide text and speaker notes |
| Text | `.txt` and `.md`, with BOM-aware decoding |
| PDF | `.pdf` searchable text layer only; OCR is not performed |
| Containers | Individual files, folders, and ZIP archives |

Folder recursion is opt-in. The CLI ignores Office temporary files and reparse points, rejects unsafe ZIP paths and excessive expansion, deduplicates canonical sources, and preserves file-level preparation failures for review. Corrupt, encrypted, image-only, unreadable, oversized, or empty documents are not submitted, while other valid documents can continue.

## Interactive workflow

The interactive **Process Documents** flow lets you add several paths, optionally enable recursion, prepare and page through results, preview the exact local JSON request, and then process the prepared items. The preview sends and writes nothing. After a successful process, results remain available to view or explicitly export to Excel.

The interactive **Preview Request** flow prepares one input, validates it through `/list`, and lets you save a request or Workbench-compatible document-record array. **Server Information** displays the current `/list` response without persisting the endpoint. **iSearch** checks health, discovers live databases, applies a configured return field set, reports bounded result cardinality and cursor metadata, distinguishes display paging from fetching the next result chunk, supports explicit sequential **Fetch All Pages**, and saves all walked query results to one atomic Excel workbook when requested.

Interactive Excel export writes a `Results` worksheet with one row per category membership. Unassigned documents receive one row with blank category fields. Export is atomic, confirms before overwriting an existing workbook, and never creates JSON sidecars or logs.

## Output and safety

Named `process` output includes a correlated `Results` Excel worksheet. It retains source metadata, preparation state, an extracted-content preview, and category membership details. Workbook text is stored safely to prevent formula injection; the header is frozen and filterable.

Both JSON artifacts and workbooks are written through a temporary sibling file and promoted only after a successful write, so cancellation or a failure does not expose a partial destination. File logs contain only lifecycle, warning/error, exit-code, and artifact-path informationâ€”never document content, request/response payloads, credentials, or server stack traces.

## Exit codes

| Code | Meaning |
| --- | --- |
| `0` | Complete success or normal interactive exit |
| `1` | Invalid command or configuration |
| `2` | Partial success with one or more file-level failures |
| `3` | Input failure or no processable documents |
| `4` | Endpoint validation or `/list` failure |
| `5` | `/cluster` request or response-contract failure |
| `6` | Report or artifact persistence failure |
| `130` | Cooperative cancellation |

## Build, test, and publish

```powershell
dotnet restore .\Carrot-CLI.slnx
dotnet build .\Carrot-CLI.slnx --no-restore
dotnet test .\Carrot-CLI.slnx --no-build --no-restore

# Example self-contained Windows publish
dotnet publish .\src\Carrot.Cli\Carrot.Cli.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  --output .\artifacts\publish\win-x64
```

The application preamble is deployed as `Content/application-preamble.md` beside the executable. Administrators can edit that Markdown file to change the startup guidance without rebuilding.

## Documentation

- [CLI reference](docs/cli-reference.md)
- [Commands and options](src/Carrot.Cli/Docs/commands-options.md)
- [Extraction rules](docs/extraction-rules.md)
- [Output format](docs/output-format.md)
- [Task Scheduler guidance](docs/task-scheduler.md)
- [Troubleshooting](docs/troubleshooting.md)
- [Third-party notices](THIRD-PARTY-NOTICES.md)
- [License](LICENSE.txt)
