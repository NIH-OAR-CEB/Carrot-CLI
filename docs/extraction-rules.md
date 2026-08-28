# Extraction rules

Supported extensions are `.docx`, `.xlsx`, `.pptx`, `.txt`, `.md`, and `.pdf`, matched case-insensitively. Folder discovery is top-level unless `--recursive` is supplied. Reparse points, Office temporary files, nested ZIPs, archive traversal paths, and absolute archive paths are rejected or ignored as appropriate.

DOCX extraction will include current visible body and table text. PPTX extraction will include slide text and speaker notes. XLSX extraction will combine nonempty cells sheet by sheet. TXT and Markdown will use BOM-aware decoding. PDF extraction will use the searchable text layer only; OCR is out of scope.

Input order is the normalized relative-path order. A corrupt, encrypted, scanned, or empty document becomes a failed report row while valid files continue. Each successfully extracted file contributes exactly one ordered Carrot document with `title` and `content` fields.
