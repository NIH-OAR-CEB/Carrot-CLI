# Clustering Settings

Interactive Process Prepared Items uses the shared clustering resolver and validator with a fixed previewed selection for this milestone.

- Algorithm is `Lingo` and language is `English`; both exact case-sensitive identifiers must be advertised as one compatible pair by `/list` before submission.
- The interactive request omits `template`, `indent`, and parameter overrides.
- The endpoint prompt defaults to `http://localhost:8080/service`; endpoints are requested for each processing attempt and never persisted.
- The configured 120-second timeout is one overall budget across retries for each API operation.
- Up to two retries apply only to stateless transient transport errors, 408, 429, and server failures.
- All ready documents are submitted together because splitting calls changes clustering semantics.

Memberships may overlap or appear under nested cluster nodes. Multiple labels on one node use ` | ` and nested levels use ` > `. Scores are retained without rounding and are relative only within that response. Empty cluster arrays are valid and produce unassigned result rows.

The reusable configuration foundation for later named commands is active:

- Without a template, omitted selections use `Lingo` and `English`; explicit algorithm and language identifiers remain case-sensitive.
- `--template` cannot be combined with `--algorithm` or `--language`. The exact template name is sent through `/cluster?template=`, and algorithm/language are omitted from the request body so the server template supplies them.
- `--parameters-file` may accompany either selection form. It must identify an existing `.json` file no larger than 1,048,576 bytes containing valid UTF-8 JSON with one object root and unique property names.
- Arbitrary nested parameter strings, numbers, Booleans, arrays, objects, and nulls are preserved exactly. The CLI does not invent algorithm-specific parameter schemas.

Named and interactive selection prompts remain deferred even though their shared resolution and validation services are implemented.

Carrot2 generally works best with roughly 100–1,000 concise documents. This recommendation is not enforced.

## Command-line usage

Display this help topic from an installed command line:

```text
Program/script: C:\Tools\Carrot CLI\carrot-cli.exe
Arguments: help clustering-settings
Start in: C:\Tools\Carrot CLI
```
