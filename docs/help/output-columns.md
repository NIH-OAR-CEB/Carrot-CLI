# Output Columns

Interactive **Save Processed Results to Excel** creates one worksheet named `Results` with these columns:

```text
RunId, RunStatus, Endpoint, Algorithm, Language, Template,
SourceOrdinal, CarrotDocumentIndex, ContainerPath, RelativePath,
FileName, Extension, SizeBytes, SHA256, ExtractionStatus, ErrorMessage,
ExtractedCharacterCount, ContentPreview, PreviewTruncated,
CategoryCount, CategoryPaths, CategoryScores, CategoryMembershipsJson
```

The workbook contains one row per submitted document, including unassigned documents; failed preparation rows are excluded. `ContentPreview` is limited to 30,000 characters from retained extracted content. Workbook-bound strings are stored as text to prevent formula injection. The interactive action writes no request/response JSON sidecar or log and confirms before replacing an existing `.xlsx` file.
