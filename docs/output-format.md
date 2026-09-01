# Output format

The interactive **Save Processed Results to Excel** action creates one worksheet named `Results` with these columns, in order:

```text
RunId, RunStatus, Endpoint, Algorithm, Language, Template,
SourceOrdinal, CarrotDocumentIndex, ContainerPath, RelativePath,
FileName, Extension, SizeBytes, SHA256, ExtractionStatus, ErrorMessage,
ExtractedCharacterCount, ContentPreview, PreviewTruncated,
CategoryCount, CategoryPaths, CategoryScores, CategoryMembershipsJson
```

The workbook has one row for every category membership in retained document and membership order. A document assigned to several categories therefore appears on several rows with identical run, source, and content fields. Every unassigned submitted document retains one row with `CategoryCount` set to `0` and blank category path and score values. Failed preparation rows were not submitted and are not included. `ContentPreview` is limited to 30,000 characters from the complete retained extracted content. Interactive export does not create request/response JSON sidecars or logs.

All workbook strings are stored as text to prevent formula injection. Numeric and Boolean columns retain their native Excel types. The header is frozen and filterable, and large content/membership columns use bounded wrapped widths.

Nested cluster labels within one node are joined with ` | ` and path levels with ` > `. The established `CategoryPaths`, `CategoryScores`, and `CategoryMembershipsJson` column names are retained, but each assigned row contains exactly one path, one optional invariant score, and one membership encoded as a singleton JSON array. `CategoryCount` is `1` for an assigned row and `0` for an unassigned row; category cells never combine multiple memberships with Excel line breaks.

The operator supplies a quoted or unquoted `.xlsx` path whose parent directory already exists. Existing files require explicit overwrite confirmation. A same-directory temporary workbook is promoted only after a complete successful save, so failure or cancellation does not expose a partial destination.

Named `preview` writes one complete indented UTF-8 request JSON artifact through the atomic temporary-sibling pattern after `/list` validation; it never calls `/cluster`. Existing JSON destinations require explicit overwrite permission, and failure or cancellation preserves the prior destination. Named process response-sidecar naming remains deferred, and current interactive workflows do not create JSON sidecars.
