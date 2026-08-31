# Preview Request

The future preview workflow will discover and extract documents, then serialize the exact Carrot clustering request without calling the `/cluster` endpoint.

In the current UI milestone:

- **Execute** displays `Pending Implementation` without reading or writing files.
- **Help** displays this topic.
- **Back to Main Menu** leaves the workflow without side effects.

Preview is intended for inspecting document ordering, extracted content, and clustering settings before a real run.

## Command-line usage

The named `preview` route remains deferred; this example records its planned unattended syntax:

```text
Program/script: C:\Tools\Carrot CLI\carrot-cli.exe
Arguments: preview --input "C:\Data\Documents" --endpoint "http://localhost:8080/service"
Start in: C:\Tools\Carrot CLI
```
