# Output format

The future workbook contains one worksheet named `Results` with these columns, in order:

```text
RunId, RunStatus, Endpoint, Algorithm, Language, Template,
SourceOrdinal, CarrotDocumentIndex, ContainerPath, RelativePath,
FileName, Extension, SizeBytes, SHA256, ExtractionStatus, ErrorMessage,
ExtractedCharacterCount, ContentPreview, PreviewTruncated,
CategoryCount, CategoryPaths, CategoryScores, CategoryMembershipsJson
```

`ContentPreview` is limited to 30,000 characters. Exact extracted content remains in the request sidecar and the exact Carrot response remains in the response sidecar. Workbook strings will be forced to text to prevent formula injection.

Nested cluster labels within one node are joined with ` | `, path levels with ` > `, and multiple memberships with Excel line breaks. Exact membership structures are preserved in `CategoryMembershipsJson`.
