# Extraction Rules

Folder discovery is top-level unless recursive discovery is selected. Reparse points and Office temporary files such as `~$document.docx` are ignored. Individual supported files are accepted directly.

ZIP inputs retain relative entry paths. Absolute paths, traversal paths, nested ZIPs, and unsafe expansion behavior are rejected.

Multiple input paths are processed in entered order, with folder and ZIP contents ordered by normalized relative path. Canonically identical sources are prepared once and later occurrences become warnings.

Each successfully extracted source file becomes exactly one in-memory Carrot document with a filename-derived `title`, complete `content`, SHA-256 hash, and contiguous document index. Corrupt, encrypted, scanned, unreadable, oversized, or empty documents become file-level failures while valid files continue. ZIP temporary content is deleted after extraction.

## Command-line usage

Display extraction and input-discovery rules:

```text
Program/script: C:\Tools\Carrot CLI\carrot-cli.exe
Arguments: help extraction-rules
Start in: C:\Tools\Carrot CLI
```
