# Commands and Options

The command surface is:

```text
carrot-cli
carrot-cli process [options]
carrot-cli preview [options]
carrot-cli server-info [options]
carrot-cli help [topic]
carrot-cli about
carrot-cli --version
```

## Process

Planned named-command options are `--input`, `--endpoint`, `--output`, `--recursive`, `--algorithm`, `--language`, `--template`, `--parameters-file`, `--timeout-seconds`, `--overwrite`, `--no-json-artifacts`, `--quiet`, and `--log-file`.

Named `process` execution remains deferred. For implemented preparation, run `carrot-cli` without arguments and select **Process Documents**. The interactive workflow accepts multiple paths and matching surrounding quotes; one Add Path action represents one file, folder, or ZIP.

## Preview

Preview uses the input, endpoint, clustering, timeout, output, recursive, and overwrite settings but never calls `/cluster`.

## Server Information

Server information accepts `--endpoint` and `--timeout-seconds` and will call `/list` when implemented.

Named operational commands never prompt. Their implementation remains deferred; Help, About, Version, and interactive document preparation are active.
