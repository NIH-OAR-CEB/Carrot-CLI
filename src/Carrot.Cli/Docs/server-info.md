# Server Information

The future server-information workflow will call the Carrot `/list` endpoint and display available algorithms, languages, and templates.

In the current UI milestone:

- **Execute** displays `Pending Implementation` without making an HTTP request.
- **Help** displays this topic.
- **Back to Main Menu** leaves the workflow without side effects.

The planned default service endpoint is `http://localhost:8080/service`.

## Command-line usage

The named `server-info` route remains deferred; this example records its planned unattended syntax:

```text
Program/script: C:\Tools\Carrot CLI\carrot-cli.exe
Arguments: server-info --endpoint "http://localhost:8080/service"
Start in: C:\Tools\Carrot CLI
```
