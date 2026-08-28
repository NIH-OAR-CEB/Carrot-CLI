# Extraction Rules

Folder discovery is top-level unless recursive discovery is selected. Reparse points and Office temporary files such as `~$document.docx` are ignored.

ZIP inputs retain relative entry paths. Absolute paths, traversal paths, nested ZIPs, and unsafe expansion behavior are rejected.

Each successfully extracted source file becomes exactly one ordered Carrot document with `title` and `content` fields. Corrupt, encrypted, scanned, or empty documents become file-level failures while valid files continue.
