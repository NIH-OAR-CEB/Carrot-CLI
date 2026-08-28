# Extraction rules

Supported extensions are `.docx`, `.xlsx`, `.pptx`, `.txt`, `.md`, and `.pdf`, matched case-insensitively. Interactive inputs may be individual files, folders, or ZIP archives. Folder discovery is top-level unless recursion is selected. Reparse points and Office temporary files are ignored; nested ZIPs, archive traversal paths, absolute archive paths, excessive expansion, and unsafe compression ratios are rejected.

DOCX extraction includes current body and table paragraphs. PPTX extraction includes slide text and speaker notes. XLSX extraction combines nonempty cells sheet by sheet. TXT and Markdown use BOM-aware decoding. PDF extraction uses the searchable text layer only; OCR is out of scope.

Multiple input paths retain user-entered order; each folder or ZIP uses normalized relative-path order. The first occurrence of a canonical source is retained and later duplicates become warnings. Corrupt, encrypted, scanned, unreadable, oversized, or empty documents become failed preparation rows while valid files continue. Each successful file contributes one ordered in-memory Carrot document with title, complete content, and SHA-256 correlation data.
