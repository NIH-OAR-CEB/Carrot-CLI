# Carrot CLI

Carrot CLI is a layout-only .NET 10 scaffold for a future Windows command-line client compatible with the Carrot 4.8.6 Document Clustering Server API. The scaffold defines commands, contracts, processing seams, extraction strategies, reporting boundaries, configuration, and future acceptance tests. Operational methods intentionally throw `NotImplementedException` and do not read documents, contact a server, serialize artifacts, or create workbooks.

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

No-argument execution is reserved for an interactive menu. `process`, `preview`, and `server-info` are noninteractive for Task Scheduler use. Interactive sessions will request an endpoint every time and will not persist it. Noninteractive endpoint resolution is planned as `--endpoint`, then `CARROTCLI_ENDPOINT`.

## Supported input and planned artifacts

The future implementation will accept a folder or ZIP archive containing `.docx`, `.xlsx`, `.pptx`, `.txt`, `.md`, and searchable `.pdf` files. Each source file becomes one Carrot document. Results will be written to a single Excel `Results` worksheet and accompanied by full `.request.json`, `.response.json`, and `.log` artifacts under an input-adjacent `Carrot Results` directory.

See [CLI reference](docs/cli-reference.md), [extraction rules](docs/extraction-rules.md), [output format](docs/output-format.md), [Task Scheduler guidance](docs/task-scheduler.md), and [troubleshooting](docs/troubleshooting.md).

## Build

```powershell
dotnet restore .\Carrot-CLI.slnx
dotnet build .\Carrot-CLI.slnx --no-restore
dotnet test .\Carrot-CLI.slnx --no-build --no-restore
```

This phase verifies the layout only. Every future operational test is explicitly skipped and names the behavior it will eventually protect.
