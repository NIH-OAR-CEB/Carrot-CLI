# Getting Started

Run `carrot-cli` without arguments to open the interactive menu.

1. Select **Process Documents**.
2. Select **Add Path** for each individual document, folder, or ZIP archive. Quoted paths are accepted.
3. Remove any mistaken paths, then select **Prepare Documents**.
4. Review successful and failed rows five at a time. Press **Escape** to return to Batch Actions without losing the batch.
5. Choose **Preview JSON Package** to inspect the complete pretty-printed local `/cluster` request one page at a time. Press **Escape** to return without sending or saving it.
6. Choose **Process Prepared Items**, confirm the `/service` endpoint, and review correlated assigned or unassigned results. Complete extracted text is sent to the selected endpoint, but processing writes no file automatically.
7. Use **View Processed Results** to reopen the latest success or **Save Processed Results to Excel** to choose an explicit `.xlsx` destination. Existing files require overwrite confirmation.
8. Select **Start Over**, **Back to Main Menu**, or **Exit** when finished.

**Preview Request** and **Server Information** still display `Pending Implementation` from their Execute actions.

For noninteractive syntax, run:

```text
carrot-cli --help
carrot-cli help commands-options
```

Noninteractive processing remains reserved for a later implementation milestone.
