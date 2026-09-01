# Preview Request

Named `preview` discovers and extracts supported input, validates the selected algorithm/language or template through `/list`, then atomically serializes the exact Carrot clustering request without calling the `/cluster` endpoint.

In the current UI milestone:

- **Interactive Preview Request** collects one file, folder, or ZIP input; optional folder recursion; a nonpersisted endpoint; and direct algorithm/language identifiers. It prepares the input, validates the selection through `/list`, displays the exact request through terminal-sized pages, and never calls `/cluster`.
- **Save Request JSON** is available from the JSON pager and writes only after an explicit path and overwrite decision. A declined or failed save leaves the request preview available.
- **Save Workbench JSON** writes only the root document array, not the `/cluster` request wrapper. Upload that `.workbench.json` file through Workbench's **Local file** source, then choose `title` and `content` as text fields.
- **Help** displays this topic.
- **Back to Main Menu** leaves the workflow without side effects.

Preview is intended for inspecting document ordering, extracted content, and clustering settings before a real run.

## Command-line usage

The named `preview` route is noninteractive and never prompts:

```text
Program/script: C:\Tools\Carrot CLI\carrot-cli.exe
Arguments: preview --input "C:\Data\Documents" --endpoint "http://localhost:8080/service"
Start in: C:\Tools\Carrot CLI
```

Without `--output`, the artifact is `<input-parent>\<input-name>.request.json`. An existing `--output` directory receives the same deterministic filename; an explicit output file is used directly. Existing artifacts require `--overwrite`. Preview returns `0` for complete output, `1` for invalid configuration or unavailable selection, `2` for useful output with file-level extraction failures, `3` when no documents are ready, `4` for `/list` failure, `6` for artifact persistence failure, and `130` for cancellation. It never calls `/cluster`.
