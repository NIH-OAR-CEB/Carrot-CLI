# Process Documents

The interactive Process Documents workflow prepares, reviews, clusters, and correlates source documents. Processing itself writes nothing automatically; the latest success can be explicitly saved to Excel.

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

Preparation safely expands ZIP archives, extracts searchable text, calculates SHA-256 hashes, and assigns contiguous document indexes to successful rows. Failed files remain visible but are excluded from processing.

Five rows are shown per page with source order, status, relative path, type, size, extracted character count, a 120-character whitespace-normalized content preview, and a concise error when applicable. Use **Next Page** and **Previous Page** to navigate; Next Page is listed first and selected by default whenever another page exists. Press **Escape** or select **Back to Batch Actions** to leave paging without discarding the prepared batch.

Batch Actions provide **View Prepared Results**, **Preview JSON Package**, **Process Prepared Items**, **Start Over**, **Help**, and **Back to Main Menu**. JSON preview displays the exact pretty-printed request body with the configured default language and algorithm and every ready document's complete title/content. It uses screen-sized pages; Next Page is selected by default, Previous Page moves backward, and Escape returns to Batch Actions. A `↪` marker identifies a display-only continuation when a long JSON string is visually wrapped. The preview excludes local source metadata, contacts no server, and writes no file.

Process Prepared Items prompts for a service endpoint ending exactly in `/service`, with `http://localhost:8080/service` prefilled but never persisted. The workflow validates exact `Lingo` and `English` identifiers through `/list`, then sends every ready document's complete extracted title and text in the exact previewed `/cluster` package. All ready documents stay in one request because splitting them would change clustering semantics. Redirects are rejected so content is not forwarded unexpectedly.

Successful processing opens five-document result pages automatically. Each row shows its submitted Carrot index, source/title, assigned or unassigned status, membership count, category paths, and unrounded scores. Memberships can overlap and can be nested. Empty cluster results are valid and show every document as unassigned. Next Page is the default whenever available; Previous Page and Escape/Back are supported. A success adds **View Processed Results** and **Save Processed Results to Excel**. A later failure leaves that previous success and the prepared batch available.

Excel export suggests a complete `Documents\carrot-results-YYYYMMDD-HHMMSS.xlsx` path that Enter accepts immediately. The suggestion remains editable, falls back to the current directory when Documents is unavailable, and accepts quoted or unquoted `.xlsx` paths under existing directories. Export confirms before replacing a file and atomically writes one `Results` row per category membership, repeating source paths, hashes, and up to 30,000 extracted characters when a document has several memberships. Unassigned documents retain one row with blank category fields. Failed preparation rows, JSON sidecars, and logs are not included. A declined or failed save leaves both the destination and retained result unchanged. Carrot2 generally works best with roughly 100–1,000 concise documents; this is guidance, not a runtime limit.

Start Over and Back require confirmation before discarding extracted content.

Use `carrot-cli help supported-formats`, `carrot-cli help extraction-rules`, and `carrot-cli help privacy` for preparation details. Named `process` command execution remains deferred.

## Command-line usage

The named `process` route remains deferred; this example records its planned unattended syntax:

```text
Program/script: C:\Tools\Carrot CLI\carrot-cli.exe
Arguments: process --input "C:\Data\Documents" --endpoint "http://localhost:8080/service"
Start in: C:\Tools\Carrot CLI
```
