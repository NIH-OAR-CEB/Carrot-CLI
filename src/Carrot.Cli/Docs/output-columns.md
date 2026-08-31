# Output Columns

Interactive **Save Processed Results to Excel** creates one worksheet named `Results` with these columns:

```text
RunId, RunStatus, Endpoint, Algorithm, Language, Template,
SourceOrdinal, CarrotDocumentIndex, ContainerPath, RelativePath,
FileName, Extension, SizeBytes, SHA256, ExtractionStatus, ErrorMessage,
ExtractedCharacterCount, ContentPreview, PreviewTruncated,
CategoryCount, CategoryPaths, CategoryScores, CategoryMembershipsJson
```

The workbook contains one row per category membership in document and membership order. Documents with several memberships repeat their run, source, and content fields on separate rows. An unassigned submitted document retains one row with `CategoryCount` equal to `0` and blank category cells; failed preparation rows are excluded. The plural category column names remain stable, but each assigned row contains one path, one optional score, and one singleton membership JSON array. `ContentPreview` is limited to 30,000 characters from retained extracted content. Workbook-bound strings are stored as text to prevent formula injection. The interactive action writes no request/response JSON sidecar or log and confirms before replacing an existing `.xlsx` file.

## Command-line usage

Display the Excel column contract before configuring an import:

```text
Program/script: C:\Tools\Carrot CLI\carrot-cli.exe
Arguments: help output-columns
Start in: C:\Tools\Carrot CLI
```
