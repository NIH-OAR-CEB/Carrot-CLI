# Clustering Settings

The future client will support algorithm, language, template, parameter-file, and timeout selections.

- Algorithm defaults to `Lingo` when no template is selected.
- Language defaults to `English` when no template is selected.
- A template can provide server-owned clustering configuration.
- A JSON parameters file can supply algorithm-specific overrides.
- Interactive endpoint values will be requested for each session and never persisted.

Available values will eventually be validated against the server's `/list` response.
