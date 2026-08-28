# Getting Started

Run `carrot-cli` without arguments to open the interactive menu.

1. Select **Process Documents**.
2. Select **Add Path** for each individual document, folder, or ZIP archive. Quoted paths are accepted.
3. Remove any mistaken paths, then select **Prepare Documents**.
4. Review successful and failed rows five at a time. Press **Escape** to return to Batch Actions without losing the batch.
5. Choose **Preview JSON Package** to inspect the complete local `/cluster` request without sending or saving it.
6. Choose **Process Prepared Items** to confirm how many documents are ready. Carrot submission remains pending.
7. Select **Start Over**, **Back to Main Menu**, or **Exit** when finished.

**Preview Request** and **Server Information** still display `Pending Implementation` from their Execute actions.

For noninteractive syntax, run:

```text
carrot-cli --help
carrot-cli help commands-options
```

Noninteractive processing and interactive Carrot submission are reserved for later implementation milestones.
