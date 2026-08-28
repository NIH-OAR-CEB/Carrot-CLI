# Windows Task Scheduler

Publish with the `WindowsTaskScheduler` profile to create a self-contained, untrimmed `win-x64` folder deployment.

Use a noninteractive command and quote every path:

```text
Program/script: C:\Tools\Carrot CLI\carrot-cli.exe
Arguments: process --input "C:\Data\Documents" --endpoint "http://localhost:8080/service" --log-file "C:\Logs\carrot-cli.log"
Start in: C:\Tools\Carrot CLI
```

The `Start in` value is required so relative configuration and log paths resolve consistently. Configure the task to record the process exit code. Codes `2` through `6` distinguish partial file failures, input failures, endpoint failures, cluster failures, and output failures; `130` indicates cancellation.
