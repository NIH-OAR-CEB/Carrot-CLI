# Output Columns

The future workbook will contain one worksheet named `Results` with these columns:

```text
RunId, RunStatus, Endpoint, Algorithm, Language, Template,
SourceOrdinal, CarrotDocumentIndex, ContainerPath, RelativePath,
FileName, Extension, SizeBytes, SHA256, ExtractionStatus, ErrorMessage,
ExtractedCharacterCount, ContentPreview, PreviewTruncated,
CategoryCount, CategoryPaths, CategoryScores, CategoryMembershipsJson
```

`ContentPreview` is limited to 30,000 characters. Full extracted content remains in the request JSON sidecar. Workbook-bound strings will be forced to text to prevent formula injection.
