# Privacy

Interactive preparation reads complete document content into application memory. Process Prepared Items sends that content to the endpoint you select. Confirm that the local workstation, visible terminal, network path, and Carrot service are approved for the source material.

- Prepared-result tables display up to 120 whitespace-normalized characters from each successful document.
- Preview JSON Package displays every ready document's full extracted title and content through bounded pages. Viewed pages may remain in terminal scrollback even though no JSON file is written.
- Paths, file metadata, extracted-character counts, warnings, and failures are displayed in the terminal and may remain in terminal scrollback.
- Starting over, returning to Main, or exiting releases the in-memory prepared batch. ZIP temporary files are removed immediately after extraction.
- Interactive endpoint values are not persisted.
- Process Prepared Items sends every ready document's full extracted title and text to the selected endpoint after `/list` validation. Redirects are rejected.
- Processed memberships and the latest successful request/response remain in memory until the batch is discarded or the application exits.
- Interactive processing writes no Excel, request/response JSON, or log files.
- Future persisted request/response JSON, logs, and Excel output can include source paths, content, metadata, previews, and failure details.

Protect terminal history and future output folders according to the sensitivity of the source documents.
