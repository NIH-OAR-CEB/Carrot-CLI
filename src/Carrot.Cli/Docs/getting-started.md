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

For noninteractive syntax, run:

```text
carrot-cli --help
carrot-cli help commands-options
```

Long help topics use terminal-sized pages. Select **Next Page** or **Previous Page** to navigate, and select **Close Help** or press **Escape** to return. Redirected help output is emitted as one complete document without prompts.

Noninteractive processing remains reserved for a later implementation milestone.

## Command-line usage

Open the Getting Started topic directly:

```text
Program/script: C:\Tools\Carrot CLI\carrot-cli.exe
Arguments: help getting-started
Start in: C:\Tools\Carrot CLI
```
