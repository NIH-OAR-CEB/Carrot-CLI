# Carrot CLI

Carrot CLI is a .NET 10 Windows command-line client compatible with the Carrot 4.8.6 Document Clustering Server API. Its interactive **Process Documents** workflow collects files, folders, and ZIP archives, extracts and reviews searchable text, validates `Lingo`/`English` through `GET /list`, submits the exact previewed package through `POST /cluster`, displays correlated results, and can explicitly save the latest success to Excel.

## Planned commands

```text
carrot-cli
carrot-cli process --input "C:\Data\Documents" --endpoint "http://localhost:8080/service"
carrot-cli preview --input "C:\Data\Documents.zip" --endpoint "http://localhost:8080/service"
carrot-cli server-info --endpoint "http://localhost:8080/service"
carrot-cli help [topic]
carrot-cli about
carrot-cli --version
```

No-argument execution displays a welcome and getting-started preamble before opening the interactive main menu. **Process Documents** opens an editable input list; **Preview Request** and **Server Information** retain their Execute, Help, and Back menus and still report `Pending Implementation`. The Help menu and `carrot-cli help [topic]` render embedded Markdown, so help remains available regardless of the working directory. Long help topics are divided into terminal-sized pages with Next Page, Previous Page, Close Help, and Escape navigation; redirected command output remains complete and unpaged.

Within **Process Documents**, select **Add Path** once for each input. Paths may be unquoted or surrounded by matching single or double quotes, for example:

```text
"C:\Data\Case Files"
'C:\Data\August documents.zip'
C:\Data\single-report.pdf
```

Before preparation, queued paths can be removed and folder recursion can be enabled. Preparation retains the first occurrence of duplicate sources, safely expands ZIPs, hashes and extracts valid documents, and keeps file-level failures visible. Results are shown five rows at a time with status, source, size, extracted character count, a short content preview, and any error. Press **Escape** from the pager to return to Batch Actions. **Preview JSON Package** pretty-prints the exact `/cluster` body and displays it through screen-sized pages, including the configured language and algorithm plus every ready document's full title and content. Long JSON strings are visually wrapped with `↪`; the value is unchanged. Next Page is the default when available, and Escape returns to Batch Actions. The preview excludes client-only paths and hashes, sends nothing, and writes no file.

**Process Prepared Items** prompts for an absolute HTTP/HTTPS endpoint ending in `/service`, prefilled with `http://localhost:8080/service`. The value is not persisted. Processing sends every ready document's complete extracted title and text to that endpoint in one request after exact `Lingo`/`English` validation. Redirects are rejected, transient stateless failures receive at most two retries within one 120-second operation budget, and failures retain the prepared batch and any previous successful result. Successful results open automatically in five-document pages showing submitted index, source/title, assigned or unassigned status, membership count, nested/overlapping category paths, and unrounded scores. Empty cluster arrays are valid and make every document unassigned. **View Processed Results** reopens the latest success. **Save Processed Results to Excel** suggests a complete `Documents\carrot-results-YYYYMMDD-HHMMSS.xlsx` destination that can be accepted with Enter or edited, confirms before overwrite, and atomically saves the retained success. Each assigned category is written on its own database-friendly row; document metadata repeats for multiple memberships, and unassigned documents retain one row with blank category fields. If an existing Documents directory is unavailable, the suggestion uses the current directory. Processing itself still writes nothing, and interactive export creates no JSON sidecar or log.

Carrot2 generally works best with roughly 100–1,000 concise documents. This is guidance only; the CLI does not enforce that range or split a prepared batch because separate calls would change clustering semantics.

The preamble source is [`docs/application-preamble.md`](docs/application-preamble.md). Builds and publishes place it at `Content/application-preamble.md` beside the application. Administrators can edit that deployed Markdown file and the next launch will display the revised text without rebuilding.

`process`, `preview`, and `server-info` remain deferred noninteractive routes intended for future Task Scheduler use. The implemented endpoint prompt belongs only to interactive **Process Prepared Items**.

## Interactive menu

```text
Main Menu
|- Process Documents -> Add/Remove Paths -> Prepare -> Review Pages -> JSON Preview / Process / Result Pages / Excel Export
|- Preview Request -> Execute | Help | Back
|- Server Information -> Execute | Help | Back
|- Help
|- About
`- Exit
```

Available help topics are getting started, process, preview, server information, commands/options, supported formats, extraction rules, clustering settings, output columns, Task Scheduler, exit codes, privacy, and troubleshooting.

## Supported input and output

Interactive preparation accepts individual files, folders, and ZIP archives containing `.docx`, `.xlsx`, `.pptx`, `.txt`, `.md`, and searchable `.pdf` files. Each successfully prepared source becomes one in-memory Carrot document; corrupt, encrypted, image-only, unreadable, and empty documents remain visible as failed rows. JSON package preview writes the complete request to terminal scrollback only. Interactive processing sends that request to the selected endpoint and retains the response in memory. An explicit Excel export writes one `Results` row per category membership with repeated document paths, hashes, and up to 30,000 extracted characters. Unassigned submitted documents receive one blank-category row; failed preparation rows are not included. JSON request/response sidecars and log artifacts remain planned for later milestones.

See [CLI reference](docs/cli-reference.md), [extraction rules](docs/extraction-rules.md), [output format](docs/output-format.md), [Task Scheduler guidance](docs/task-scheduler.md), and [troubleshooting](docs/troubleshooting.md).

## Build

```powershell
dotnet restore .\Carrot-CLI.slnx
dotnet build .\Carrot-CLI.slnx --no-restore
dotnet test .\Carrot-CLI.slnx --no-build --no-restore
```

The active test suite verifies command metadata, preamble loading, interactive navigation, quoted paths, folder and ZIP safety, all supported extractors, deduplication, preparation outcomes, endpoint validation, HTTP request contracts, redirect/retry/timeout/cancellation behavior, recursive membership mapping, source correlation, retained-result state, paging/Escape behavior, output-path and overwrite decisions, deterministic report mapping, atomic formula-safe workbooks, dependency injection, embedded resources, Markdown escaping, and Help/About routes. Named-command and architecture-scanner acceptances remain deferred.
