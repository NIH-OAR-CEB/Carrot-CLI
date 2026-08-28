# Windows Task Scheduler

Task Scheduler must use a named, noninteractive command. Quote all paths and configure the executable folder as **Start in**.

```text
Program/script: C:\Tools\Carrot CLI\carrot-cli.exe
Arguments: process --input "C:\Data\Documents" --endpoint "http://localhost:8080/service"
Start in: C:\Tools\Carrot CLI
```

Record the process exit code and provide an explicit log path for unattended diagnostics. Do not schedule no-argument interactive execution.
