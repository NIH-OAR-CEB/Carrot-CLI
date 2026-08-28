# Carrot CLI

Carrot CLI is a .NET 10 command-line client scaffold for a future Windows client compatible with the Carrot 4.8.6 Document Clustering Server API. Its initial Spectre.Console UI is runnable: no arguments open selectable menus, every workflow menu includes Markdown-backed help, and Help and About are also available as named commands. Document discovery, extraction, server calls, and report generation remain explicit operational stubs.

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

No-argument execution displays a welcome and getting-started preamble before opening the interactive main menu. Process Documents, Preview Request, and Server Information each open a submenu containing Execute, Help, and Back. Execute currently displays `Pending Implementation` without invoking an operational stub. The Help menu and `carrot-cli help [topic]` render embedded Markdown, so help remains available regardless of the working directory.

The preamble source is [`docs/application-preamble.md`](docs/application-preamble.md). Builds and publishes place it at `Content/application-preamble.md` beside the application. Administrators can edit that deployed Markdown file and the next launch will display the revised text without rebuilding.

`process`, `preview`, and `server-info` remain deferred noninteractive routes intended for future Task Scheduler use. Interactive endpoint prompts and noninteractive endpoint resolution are not part of this UI milestone.

## Interactive menu

```text
Main Menu
|- Process Documents -> Execute | Help | Back
|- Preview Request -> Execute | Help | Back
|- Server Information -> Execute | Help | Back
|- Help
|- About
`- Exit
```

Available help topics are getting started, process, preview, server information, commands/options, supported formats, extraction rules, clustering settings, output columns, Task Scheduler, exit codes, privacy, and troubleshooting.

## Supported input and planned artifacts

The future implementation will accept a folder or ZIP archive containing `.docx`, `.xlsx`, `.pptx`, `.txt`, `.md`, and searchable `.pdf` files. Each source file becomes one Carrot document. Results will be written to a single Excel `Results` worksheet and accompanied by full `.request.json`, `.response.json`, and `.log` artifacts under an input-adjacent `Carrot Results` directory.

See [CLI reference](docs/cli-reference.md), [extraction rules](docs/extraction-rules.md), [output format](docs/output-format.md), [Task Scheduler guidance](docs/task-scheduler.md), and [troubleshooting](docs/troubleshooting.md).

## Build

```powershell
dotnet restore .\Carrot-CLI.slnx
dotnet build .\Carrot-CLI.slnx --no-restore
dotnet test .\Carrot-CLI.slnx --no-build --no-restore
```

The active test suite verifies command metadata, preamble loading and placement, interactive navigation, pending workflow behavior, dependency injection, embedded resources, Markdown escaping, and Help/About routes. Future operational acceptance tests remain explicitly skipped and name the behavior they will eventually protect.
