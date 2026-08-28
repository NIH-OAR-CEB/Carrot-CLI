# Commands and Options

The planned command surface is:

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

Planned options are `--input`, `--endpoint`, `--output`, `--recursive`, `--algorithm`, `--language`, `--template`, `--parameters-file`, `--timeout-seconds`, `--overwrite`, `--no-json-artifacts`, `--quiet`, and `--log-file`.

## Preview

Preview uses the input, endpoint, clustering, timeout, output, recursive, and overwrite settings but never calls `/cluster`.

## Server Information

Server information accepts `--endpoint` and `--timeout-seconds` and will call `/list` when implemented.

Named operational commands never prompt. Their implementation remains deferred.
