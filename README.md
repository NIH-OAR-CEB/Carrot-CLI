# Carrot CLI

Carrot CLI is a .NET 10 Windows command-line client compatible with the Carrot 4.8.6 Document Clustering Server API. Its interactive **Process Documents** workflow can collect multiple files, folders, and ZIP archives, safely discover supported documents, extract searchable text, and review the prepared rows before processing. Carrot submission and report generation remain deferred.

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

No-argument execution displays a welcome and getting-started preamble before opening the interactive main menu. **Process Documents** opens an editable input list; **Preview Request** and **Server Information** retain their Execute, Help, and Back menus and still report `Pending Implementation`. The Help menu and `carrot-cli help [topic]` render embedded Markdown, so help remains available regardless of the working directory.

Within **Process Documents**, select **Add Path** once for each input. Paths may be unquoted or surrounded by matching single or double quotes, for example:

```text
"C:\Data\Case Files"
'C:\Data\August documents.zip'
C:\Data\single-report.pdf
```

Before preparation, queued paths can be removed and folder recursion can be enabled. Preparation retains the first occurrence of duplicate sources, safely expands ZIPs, hashes and extracts valid documents, and keeps file-level failures visible. Results are shown five rows at a time with status, source, size, extracted character count, a short content preview, and any error. Press **Escape** from the pager to return to Batch Actions. **Preview JSON Package** pretty-prints the exact `/cluster` body and displays it through screen-sized pages, including the configured language and algorithm plus every ready document's full title and content. Long JSON strings are visually wrapped with `↪`; the value is unchanged. Next Page is the default when available, and Escape returns to Batch Actions. The preview excludes client-only paths and hashes, sends nothing, and writes no file. **Process Prepared Items** reports how many documents are ready but does not yet contact Carrot or create artifacts.

The preamble source is [`docs/application-preamble.md`](docs/application-preamble.md). Builds and publishes place it at `Content/application-preamble.md` beside the application. Administrators can edit that deployed Markdown file and the next launch will display the revised text without rebuilding.

`process`, `preview`, and `server-info` remain deferred noninteractive routes intended for future Task Scheduler use. Interactive endpoint prompts and noninteractive endpoint resolution are not part of this UI milestone.

## Interactive menu

```text
Main Menu
|- Process Documents -> Add/Remove Paths -> Prepare -> Review Pages -> JSON Preview / Batch Actions
|- Preview Request -> Execute | Help | Back
|- Server Information -> Execute | Help | Back
|- Help
|- About
`- Exit
```

Available help topics are getting started, process, preview, server information, commands/options, supported formats, extraction rules, clustering settings, output columns, Task Scheduler, exit codes, privacy, and troubleshooting.

## Supported input and planned artifacts

Interactive preparation accepts individual files, folders, and ZIP archives containing `.docx`, `.xlsx`, `.pptx`, `.txt`, `.md`, and searchable `.pdf` files. Each successfully prepared source becomes one in-memory Carrot document; corrupt, encrypted, image-only, unreadable, and empty documents remain visible as failed rows. JSON package preview writes the complete request to terminal scrollback only; no preparation artifact is written. A later milestone will submit the retained documents and write the planned Excel, JSON, and log artifacts.

See [CLI reference](docs/cli-reference.md), [extraction rules](docs/extraction-rules.md), [output format](docs/output-format.md), [Task Scheduler guidance](docs/task-scheduler.md), and [troubleshooting](docs/troubleshooting.md).

## Build

```powershell
dotnet restore .\Carrot-CLI.slnx
dotnet build .\Carrot-CLI.slnx --no-restore
dotnet test .\Carrot-CLI.slnx --no-build --no-restore
```

The active test suite verifies command metadata, preamble loading, interactive navigation, quoted paths, folder and ZIP safety, all supported extractors, deduplication, preparation outcomes, paging/Escape behavior, dependency injection, embedded resources, Markdown escaping, and Help/About routes. Deferred clustering and reporting acceptance tests remain explicitly skipped and name the behavior they will eventually protect.
