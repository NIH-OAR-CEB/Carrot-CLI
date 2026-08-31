# Clustering Settings

Interactive Process Prepared Items uses a fixed previewed configuration for this milestone.

- Algorithm is `Lingo` and language is `English`; both exact identifiers must be advertised together by `/list` before submission.
- The interactive request omits `template`, `indent`, and parameter overrides.
- The endpoint prompt defaults to `http://localhost:8080/service`; endpoints are requested for each processing attempt and never persisted.
- The configured 120-second timeout is one overall budget across retries for each API operation.
- Up to two retries apply only to stateless transient transport errors, 408, 429, and server failures.
- All ready documents are submitted together because splitting calls changes clustering semantics.

Memberships may overlap or appear under nested cluster nodes. Multiple labels on one node use ` | ` and nested levels use ` > `. Scores are retained without rounding and are relative only within that response. Empty cluster arrays are valid and produce unassigned result rows.

Carrot2 generally works best with roughly 100–1,000 concise documents. This recommendation is not enforced. Algorithm/template selection and parameter files remain deferred to later interactive or named-command milestones.
