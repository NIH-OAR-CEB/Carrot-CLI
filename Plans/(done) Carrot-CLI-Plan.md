# Carrot CLI Layout-Only Scaffold

## Summary

Create a documented, buildable .NET 10 solution skeleton at `C:\Source\Programs\Carrot CLI\Carrot-CLI`. It will contain method signatures, DTOs, interfaces, command definitions, XML documentation, and `NotImplementedException` bodies only—no parsing, HTTP, JSON, or Excel implementation.

The eventual pipeline will:

1. Accept a folder or ZIP archive.
2. Treat each supported file as one Carrot document.
3. Extract searchable text.
4. Build one ordered Carrot `/service/cluster` request.
5. Match response document indexes back to source files.
6. Flatten all nested category paths.
7. Produce one Excel `Results` worksheet plus full request/response JSON sidecars.

The local Carrot 4.8.6 OpenAPI document at `C:\carrot2\dcs\web\service\openapi\dcs.yaml` is the authoritative API contract.

## File Layout

```text
Carrot-CLI/
├── Carrot-CLI.slnx
├── Directory.Packages.props
├── README.md
├── THIRD-PARTY-NOTICES.md
├── docs/
│   ├── cli-reference.md
│   ├── extraction-rules.md
│   ├── output-format.md
│   ├── task-scheduler.md
│   └── troubleshooting.md
├── src/
│   └── Carrot.Cli/
│       ├── Carrot.Cli.csproj
│       ├── Program.cs
│       ├── appsettings.json
│       ├── Properties/PublishProfiles/WindowsTaskScheduler.pubxml
│       ├── Composition/
│       │   └── ServiceRegistration.cs
│       ├── Cli/
│       │   ├── CommandAppFactory.cs
│       │   ├── DependencyInjection/
│       │   │   ├── TypeRegistrar.cs
│       │   │   └── TypeResolver.cs
│       │   ├── Commands/
│       │   │   ├── InteractiveCommand.cs
│       │   │   ├── ProcessCommand.cs
│       │   │   ├── PreviewCommand.cs
│       │   │   ├── ServerInfoCommand.cs
│       │   │   ├── HelpCommand.cs
│       │   │   └── AboutCommand.cs
│       │   ├── Settings/
│       │   │   ├── InputSettings.cs
│       │   │   ├── ClusteringSettings.cs
│       │   │   ├── ProcessSettings.cs
│       │   │   ├── PreviewSettings.cs
│       │   │   └── EndpointSettings.cs
│       │   └── UI/
│       │       ├── InteractiveMenu.cs
│       │       ├── ConsoleReporter.cs
│       │       ├── HelpRenderer.cs
│       │       └── AboutRenderer.cs
│       ├── Configuration/
│       │   ├── CarrotCliOptions.cs
│       │   ├── CarrotCliOptionsValidator.cs
│       │   └── RunSettingsResolver.cs
│       ├── Processing/
│       │   ├── IDocumentProcessingWorkflow.cs
│       │   ├── DocumentProcessingWorkflow.cs
│       │   ├── ProcessRequest.cs
│       │   ├── PreviewRequest.cs
│       │   ├── ProcessRunResult.cs
│       │   └── ExitCodes.cs
│       ├── Input/
│       │   ├── IInputSourceLoader.cs
│       │   ├── InputSourceResolver.cs
│       │   ├── FolderInputSourceLoader.cs
│       │   ├── ZipInputSourceLoader.cs
│       │   ├── InputBatch.cs
│       │   └── SourceFile.cs
│       ├── Extraction/
│       │   ├── IDocumentTextExtractor.cs
│       │   ├── DocumentExtractionCoordinator.cs
│       │   ├── ExtractedDocument.cs
│       │   ├── ExtractionResult.cs
│       │   └── Extractors/
│       │       ├── PlainTextExtractor.cs
│       │       ├── WordDocumentExtractor.cs
│       │       ├── SpreadsheetExtractor.cs
│       │       ├── PresentationExtractor.cs
│       │       └── PdfDocumentExtractor.cs
│       ├── CarrotApi/
│       │   ├── ICarrotApiClient.cs
│       │   ├── CarrotApiClient.cs
│       │   ├── EndpointResolver.cs
│       │   ├── ClusterMembershipMapper.cs
│       │   └── Contracts/
│       │       ├── ClusterRequest.cs
│       │       ├── ClusterDocument.cs
│       │       ├── ClusterResponse.cs
│       │       ├── ClusterNode.cs
│       │       ├── ListResponse.cs
│       │       └── CarrotErrorResponse.cs
│       ├── Reporting/
│       │   ├── IExcelReportWriter.cs
│       │   ├── ExcelReportWriter.cs
│       │   ├── IJsonArtifactWriter.cs
│       │   ├── JsonArtifactWriter.cs
│       │   ├── ReportRequest.cs
│       │   ├── ReportRow.cs
│       │   └── ClusterMembership.cs
│       └── Common/
│           ├── OperationResult.cs
│           ├── OperationMessage.cs
│           ├── OperationStatus.cs
│           ├── HashService.cs
│           └── AtomicFileWriter.cs
└── tests/
    └── Carrot.Cli.Tests/
        ├── Carrot.Cli.Tests.csproj
        ├── Cli/CommandContractTests.cs
        ├── Input/InputDiscoveryTests.cs
        ├── Extraction/DocumentExtractorTests.cs
        ├── CarrotApi/ContractSerializationTests.cs
        ├── CarrotApi/ClusterMembershipMapperTests.cs
        ├── Reporting/ExcelReportWriterTests.cs
        ├── Processing/DocumentProcessingWorkflowTests.cs
        ├── Architecture/DocumentationConventionTests.cs
        └── Fixtures/Samples/
            ├── Documents/
            ├── Archives/
            └── ApiResponses/
```

Use centrally pinned packages: Spectre.Console/Cli 0.55.0, Microsoft.Extensions.Hosting 10.0.11, DocumentFormat.OpenXml 3.5.1, ClosedXML 0.105.1, PdfPig 0.1.16, Serilog.Extensions.Hosting 10.0.0, Serilog.Sinks.File 7.0.0, xUnit v3 4.0.0, and Microsoft.NET.Test.Sdk 18.9.0. Spectre.Console/Cli 0.55.0 replaces the originally proposed 0.54.0 because the CLI package is not published at 0.54.0 and NuGet otherwise resolves an incompatible mixed-version graph. These choices align with the official [Spectre CLI](https://spectreconsole.net/cli/), [Open XML SDK](https://www.nuget.org/packages/DocumentFormat.OpenXml), [ClosedXML](https://www.nuget.org/packages/ClosedXML), and [PdfPig](https://www.nuget.org/packages/PdfPig) guidance.

## Command and Stub Contracts

### User-facing commands

```text
carrot-cli
carrot-cli process [options]
carrot-cli preview [options]
carrot-cli server-info [options]
carrot-cli help [topic]
carrot-cli about
carrot-cli --version
```

- No arguments launch the Spectre menu.
- `process`, `preview`, and `server-info` never prompt, making them Task Scheduler-safe.
- Interactive sessions prompt for the endpoint every time, prefilled with `http://localhost:8080/service`; it is never persisted.
- Noninteractive endpoint precedence is `--endpoint` followed by `CARROTCLI_ENDPOINT`. Missing endpoint returns a configuration error.
- `process` options: `--input`, `--endpoint`, `--output`, `--recursive`, `--algorithm`, `--language`, `--template`, `--parameters-file`, `--timeout-seconds`, `--overwrite`, `--no-json-artifacts`, `--quiet`, and `--log-file`.
- Algorithm and language default to `Lingo` and `English` when no template is selected.
- `preview` performs discovery, extraction, and request serialization without calling `/cluster`.
- `server-info` calls `/list` and displays algorithms, languages, and templates.
- Help topics: getting started, commands/options, supported formats, extraction rules, clustering settings, output columns, Task Scheduler, exit codes, privacy, and troubleshooting.
- About displays application/version information, supported formats, Carrot 4.8.6 compatibility, reference links, and third-party notices.

### Primary internal seams

- `Program.Main(string[] args)` creates the Generic Host, composition root, logging, and Spectre command app.
- `IDocumentProcessingWorkflow.ProcessAsync(ProcessRequest, CancellationToken)` coordinates the complete run.
- `IDocumentProcessingWorkflow.PreviewAsync(PreviewRequest, CancellationToken)` creates a payload without submission.
- `IInputSourceLoader.LoadAsync(...)` returns an `InputBatch` owning any temporary ZIP extraction directory.
- `IDocumentTextExtractor.ExtractAsync(SourceFile, CancellationToken)` returns extracted text or a structured file-level failure.
- `ICarrotApiClient.GetConfigurationAsync(...)` models `/list`.
- `ICarrotApiClient.ClusterAsync(...)` models `/cluster`.
- `ClusterMembershipMapper.Map(...)` recursively validates indexes and produces full category paths.
- `IExcelReportWriter.WriteAsync(...)` defines the single-sheet report boundary.
- `IJsonArtifactWriter.WriteAsync(...)` defines atomic request/response JSON persistence.
- `OperationResult<T>` uses immutable messages and `Success`, `PartialSuccess`, and `Failure` states; expected file failures do not use exceptions.

All non-entry-point types should be `internal` by default. Volatile dependencies—HTTP, file access, clock, reporting, and logging—are constructor-injected and registered through feature-specific `Add*` methods.

### Documentation-only rules

Every C# class, record, interface, method, and property receives:

- The `/**************************************************************/` separator.
- XML `<summary>` documentation.
- `<remarks>`, parameters, return values, exceptions, examples, and `<seealso>` references where applicable.
- Lowercase private method names and PascalCase public methods.
- Lowercase `#region implementation` blocks.
- Stub bodies containing only `throw new NotImplementedException("Layout stub only.");`.

No stub will enumerate files, extract text, contact Carrot, serialize JSON, or create a workbook.

## Planned Processing and Output Behavior

- Supported extensions are `.docx`, `.xlsx`, `.pptx`, `.txt`, `.md`, and `.pdf`, matched case-insensitively.
- Each file is exactly one Carrot document. ZIP entries retain their relative archive path.
- Folder discovery is top-level by default; `--recursive` opts into subfolders. Reparse points are not followed.
- Office temporary files such as `~$*.docx` are ignored.
- Input order is deterministic by normalized relative path. Extraction failures retain their source ordinal but receive no Carrot document index.
- DOCX extracts current visible body/table text; PPTX extracts slides and speaker notes; XLSX combines nonempty cells sheet-by-sheet; TXT/MD use BOM-aware text decoding; PDF uses its searchable text layer only.
- Scanned PDFs, encrypted files, corrupt files, and empty documents become failed report rows. Valid files continue and cause exit code `2`.
- The Carrot document fields are `title` and `content`; correlation metadata remains client-side so paths and hashes do not influence clustering.
- Save the complete request JSON before submission. Submit all valid documents in one call because splitting requests would change global clustering semantics.
- Validate the endpoint and selected configuration through `/list`, then post to `/cluster`.
- Retry only transient, stateless failures; do not retry `400` responses.
- Validate every response document index before mapping. Out-of-range indexes invalidate the response.
- Map every matching cluster node, including nested nodes. Join labels within a node using ` | `, path levels using ` > `, and separate memberships using Excel line breaks.
- Preserve exact membership data in `CategoryMembershipsJson`.

The single `Results` worksheet will contain:

```text
RunId, RunStatus, Endpoint, Algorithm, Language, Template,
SourceOrdinal, CarrotDocumentIndex, ContainerPath, RelativePath,
FileName, Extension, SizeBytes, SHA256, ExtractionStatus, ErrorMessage,
ExtractedCharacterCount, ContentPreview, PreviewTruncated,
CategoryCount, CategoryPaths, CategoryScores, CategoryMembershipsJson
```

`ContentPreview` is limited to 30,000 characters. Full extracted text remains in the `.request.json` sidecar. The exact Carrot response is saved as `.response.json`. Excel-bound strings are written as text to prevent formula injection.

Default artifacts are written atomically under:

```text
<input-parent>\Carrot Results\
    <input-name>-carrot-<timestamp>-<run-id>.xlsx
    <input-name>-carrot-<timestamp>-<run-id>.request.json
    <input-name>-carrot-<timestamp>-<run-id>.response.json
    <input-name>-carrot-<timestamp>-<run-id>.log
```

Default safeguards in `appsettings.json`: 10,000 files/archive entries, 100 MB per file, 1 GB expanded ZIP size, 50 million total extracted characters, four extraction workers, 120-second HTTP timeout, and two transient retries. ZIP traversal, absolute paths, nested ZIPs, and excessive compression ratios are rejected.

Exit codes:

```text
0   Complete success
1   Invalid command or configuration
2   Partial success with one or more file failures
3   No processable documents/input failure
4   Endpoint or /list failure
5   /cluster request or response-contract failure
6   Report/artifact write failure
130 Cancellation
```

The Task Scheduler publish profile produces a self-contained, untrimmed `win-x64` folder deployment. Documentation includes quoted command examples, a required “Start in” directory, log locations, and exit-code handling.

## Test Plan and Assumptions

- Layout-only verification: solution restores and builds on .NET 10; every planned type and signature exists; XML documentation is present; operational bodies remain `NotImplementedException`.
- Scaffold test classes contain skipped xUnit facts naming future acceptance scenarios—no behavioral implementation in this phase.
- Future tests cover deterministic folder/ZIP discovery, traversal defenses, each extractor, corrupt/encrypted/image-only files, payload ordering, OpenAPI serialization, nested/multiple category mapping, unassigned documents, invalid indexes, Excel columns and preview truncation, formula safety, atomic output, retries, cancellation, CLI help, and exit codes.
- Add one future end-to-end test using mixed fixture files and a fake HTTP handler; no live Carrot server is required in unit tests.
- The local Carrot 4.8.6 OpenAPI file is authoritative; the web search UI is informational.
- No OCR, authentication, nested archive processing, individual-file input mode, Carrot server management, automatic API batching, or saved endpoint profile is included.

## Completion Evidence

Completed on 2026-08-28 as a layout-only scaffold. The solution contains every planned production and test file, OpenAPI-aligned request/response DTOs, documented command and workflow signatures, strongly typed configuration, feature boundaries, immutable operation-result contracts, a self-contained Windows Task Scheduler publish profile, user documentation, and 19 explicitly skipped future acceptance scenarios. Operational methods remain `NotImplementedException("Layout stub only.")` stubs.

The originally proposed Spectre.Console/Cli 0.54.0 pair was corrected to 0.55.0 after NuGet proved that `Spectre.Console.Cli` 0.54.0 is not published on the configured feed and resolves to 0.55.0, which requires Spectre.Console 0.55.0. The compatible pair is centrally pinned in `Directory.Packages.props`.

Verification completed with:

- `dotnet restore .\Carrot-CLI.slnx --disable-parallel`
- `dotnet build .\Carrot-CLI.slnx --no-restore --disable-build-servers -m:1 --verbosity minimal` — 0 warnings and 0 errors.
- `dotnet test .\Carrot-CLI.slnx --no-build --no-restore --verbosity normal` — 19 total future tests, 19 intentionally skipped, 0 failed.
- `dotnet restore .\src\Carrot.Cli\Carrot.Cli.csproj -r win-x64 --disable-parallel`
- `dotnet publish .\src\Carrot.Cli\Carrot.Cli.csproj --no-restore --disable-build-servers -m:1 -p:PublishProfile=WindowsTaskScheduler --verbosity minimal` — succeeded with a self-contained, untrimmed `win-x64` folder output.
- `git diff --check` — no whitespace errors; Git reported only expected line-ending normalization notices for pre-existing tracked files.
