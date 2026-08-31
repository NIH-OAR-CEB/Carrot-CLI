# Supported Formats

Interactive preparation accepts these extensions case-insensitively:

- `.docx` — Word document body and table text.
- `.xlsx` — Nonempty spreadsheet cells, sheet by sheet.
- `.pptx` — Slide text and speaker notes.
- `.txt` — BOM-aware plain text.
- `.md` — BOM-aware Markdown source text.
- `.pdf` — Searchable text layer only.

Inputs may be individual supported files, folders, or ZIP archives. Nested ZIPs are rejected. OCR is not included; image-only or scanned PDFs become failed preparation rows unless they already contain a searchable text layer.

## Command-line usage

Display the supported input-format reference:

```text
Program/script: C:\Tools\Carrot CLI\carrot-cli.exe
Arguments: help supported-formats
Start in: C:\Tools\Carrot CLI
```
