# Supported Formats

Interactive preparation accepts these extensions case-insensitively:

- `.docx` — Word document body and table text.
- `.xlsx` — Nonempty spreadsheet cells, sheet by sheet.
- `.pptx` — Slide text and speaker notes.
- `.txt` — BOM-aware plain text.
- `.md` — BOM-aware Markdown source text.
- `.pdf` — Searchable text layer only.

Inputs may be individual supported files, folders, or ZIP archives. Nested ZIPs are rejected. OCR is not included; image-only or scanned PDFs become failed preparation rows unless they already contain a searchable text layer.
