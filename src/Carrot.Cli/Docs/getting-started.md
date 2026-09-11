# Getting Started

Run `carrot-cli` without arguments to open the interactive menu.

1. Select **Process Documents**.
2. Select **Add Path** for each individual document, folder, or ZIP archive. Quoted paths are accepted.
3. Remove any mistaken paths, then select **Prepare Documents**.
4. Review successful and failed rows five at a time. Press **Escape** to return to Batch Actions without losing the batch.
5. Choose **Preview JSON Package** to inspect the complete pretty-printed local `/cluster` request one page at a time. Press **Escape** to return without sending or saving it.
6. Choose **Process Prepared Items**, confirm the `/service` endpoint, and review correlated assigned or unassigned results. Complete extracted text is sent to the selected endpoint, but processing writes no file automatically.
7. Select **Save Processed Results to Excel** from any processed-results page or from Batch Actions to accept the suggested timestamped `.xlsx` destination with Enter. Saving returns to the current results page. Use **View Processed Results** to reopen the latest success. The suggestion can be edited, and existing files require overwrite confirmation.
8. Select **Start Over**, **Back to Main Menu**, or **Exit** when finished.

The interactive **Preview Request** flow collects one input, endpoint, algorithm, and language; prepares the input, validates through `/list`, and pages the exact local request without calling `/cluster`. **Save Request JSON** is explicit, so a declined or failed save leaves the preview available. **Server Information** prompts for the nonpersisted Carrot endpoint (default `http://localhost:8080/service`) and displays the server's algorithms, languages, and templates from `/list`.

The optional **iSearch** workflow requires `iSearch:apiKey` and `iSearch:contactEmail` in User Secrets. It checks `/health`, discovers live databases, requires a configured return dataset under `iSearchReturnTypes.Results`, and submits selected-dataset queries with that group's ordered fields and a 100-record limit. **Build Advanced Query** uses live fields to guide `q`, `qf`, `fq`, and updated-date bounds, then displays a pretty JSON package for edit or confirmation before submission. Results include common total/current/result-page cardinality and retain the iSearch cursor. **Next Display Page** moves through terminal lines locally; **Fetch Next Result Page** fetches one additional data chunk; **Fetch All Pages**, immediately below it, walks every remaining chunk sequentially while the Search Summary updates Data Page and actual loaded-record percentage. **Save iSearch Results to Excel** combines every page fetched during the current query visit and does not fetch more data. Continuations respect the documented authenticated one-second throttle. The key is sent only as a cookie, and saving is explicit, local, atomic, and creates no sidecar or log; see [iSearch](isearch.md).

For unattended use, the named `isearch` command accepts the same advanced controls without prompts. Use `--query`, repeat `--query-field` and `--filter-query`, `--default-op`, `--rows`, `--updated-after`, and `--updated-before`; `--result-dataset` selects the configured `fl` fields. Use `--max-results` for a bounded walk or explicit `--all-results` for complete paging. See [iSearch command-line usage](isearch.md#command-line-usage) for examples and exit codes.

For noninteractive syntax, run:

```text
carrot-cli --help
carrot-cli help commands-options
```

Long help topics use terminal-sized pages. Select **Next Page** or **Previous Page** to navigate, and select **Close Help** or press **Escape** to return. Redirected help output is emitted as one complete document without prompts.

The named `isearch` operation is noninteractive and suitable for scripts or scheduled tasks. Other noninteractive processing operations remain reserved for later implementation milestones.

## Command-line usage

Open the Getting Started topic directly:

```text
Program/script: C:\Tools\Carrot CLI\carrot-cli.exe
Arguments: help getting-started
Start in: C:\Tools\Carrot CLI
```
