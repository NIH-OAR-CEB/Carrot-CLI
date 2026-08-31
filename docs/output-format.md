# Output format

The interactive **Save Processed Results to Excel** action creates one worksheet named `Results` with these columns, in order:

```text
RunId, RunStatus, Endpoint, Algorithm, Language, Template,
SourceOrdinal, CarrotDocumentIndex, ContainerPath, RelativePath,
FileName, Extension, SizeBytes, SHA256, ExtractionStatus, ErrorMessage,
ExtractedCharacterCount, ContentPreview, PreviewTruncated,
CategoryCount, CategoryPaths, CategoryScores, CategoryMembershipsJson
```

The workbook has one row for every document submitted in the retained successful Carrot request, including unassigned documents. Failed preparation rows were not submitted and are not included. `ContentPreview` is limited to 30,000 characters from the complete retained extracted content. Interactive export does not create request/response JSON sidecars or logs.

All workbook strings are stored as text to prevent formula injection. Numeric and Boolean columns retain their native Excel types. The header is frozen and filterable, and large content/membership columns use bounded wrapped widths.

Nested cluster labels within one node are joined with ` | `, path levels with ` > `, and multiple memberships with Excel line breaks. Exact membership structures are preserved in `CategoryMembershipsJson`.

The operator supplies a quoted or unquoted `.xlsx` path whose parent directory already exists. Existing files require explicit overwrite confirmation. A same-directory temporary workbook is promoted only after a complete successful save, so failure or cancellation does not expose a partial destination.
