# Process Documents

The interactive Process Documents workflow prepares and reviews source documents without contacting a Carrot server or writing output artifacts.

## Build the input batch

Select **Add Path** once for each individual document, folder, or ZIP archive. Matching surrounding single or double quotes are accepted, including:

```text
"C:\Data\Case Files"
'C:\Data\August documents.zip'
C:\Data\single-report.pdf
```

Use **Remove Path** to correct the list. When at least one path is queued, select **Prepare Documents**. If the batch contains folders, choose whether all folders should include subfolders; the default is top-level discovery only.

Inputs are processed in entered order. Documents inside each folder or ZIP are ordered by relative path. If the same source is discovered again, the first occurrence is retained and later occurrences are reported as warnings.

## Review prepared rows

Preparation safely expands ZIP archives, extracts searchable text, calculates SHA-256 hashes, and assigns contiguous document indexes to successful rows. Failed files remain visible but are excluded from the future processing collection.

Five rows are shown per page with source order, status, relative path, type, size, extracted character count, a 120-character whitespace-normalized content preview, and a concise error when applicable. Use **Previous Page** and **Next Page** to navigate. Press **Escape** or select **Back to Batch Actions** to leave paging without discarding the prepared batch.

Batch Actions provide **View Prepared Results**, **Process Prepared Items**, **Start Over**, **Help**, and **Back to Main Menu**. Processing reports the number of ready documents but remains pending; it does not contact Carrot or create Excel, JSON, or log files. Start Over and Back require confirmation before discarding extracted content.

Use `carrot-cli help supported-formats`, `carrot-cli help extraction-rules`, and `carrot-cli help privacy` for preparation details. Named `process` command execution remains deferred.
