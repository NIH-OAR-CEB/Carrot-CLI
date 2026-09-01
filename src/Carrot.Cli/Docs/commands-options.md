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

Clustering option resolution is implemented for those later commands. Without `--template`, omitted algorithm and language values default to exact `Lingo` and `English` identifiers. `--template` is mutually exclusive with both direct-selection options and delegates algorithm/language to the exact advertised server template. A `--parameters-file` may accompany either form and must be an existing `.json` file of at most 1,048,576 bytes containing valid UTF-8 JSON with one object root and unique property names.

Named `process` execution remains deferred. For implemented preparation, run `carrot-cli` without arguments and select **Process Documents**. The interactive workflow accepts multiple paths and matching surrounding quotes; one Add Path action represents one file, folder, or ZIP.

## Preview

Preview uses the input, endpoint, clustering, timeout, output, recursive, and overwrite settings but never calls `/cluster`.

## Server Information

Server information accepts `--endpoint` and `--timeout-seconds` and will call `/list` when implemented.

Named operational commands never prompt. Their execution remains deferred; shared settings validation, clustering resolution, exact `/list` validation, Help, About, Version, and interactive document processing are active.

## Command-line usage

Display the complete command and option reference:

```text
Program/script: C:\Tools\Carrot CLI\carrot-cli.exe
Arguments: help commands-options
Start in: C:\Tools\Carrot CLI
```
