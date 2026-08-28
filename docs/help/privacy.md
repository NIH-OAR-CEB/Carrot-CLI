# Privacy

Interactive preparation reads complete document content into application memory. It does not send content to a server or write preparation artifacts. Confirm that the local workstation and visible terminal are approved for the source material.

- Prepared-result tables display up to 120 whitespace-normalized characters from each successful document.
- Paths, file metadata, extracted-character counts, warnings, and failures are displayed in the terminal and may remain in terminal scrollback.
- Starting over, returning to Main, or exiting releases the in-memory prepared batch. ZIP temporary files are removed immediately after extraction.
- Interactive endpoint values are not persisted.
- Future processing will send full extracted text to the configured Carrot server.
- Future request/response JSON, logs, and Excel output can include source paths, content, metadata, previews, and failure details.

Protect terminal history and future output folders according to the sensitivity of the source documents.
