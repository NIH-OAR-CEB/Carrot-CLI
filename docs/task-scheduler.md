# Windows Task Scheduler

Publish with the `WindowsTaskScheduler` profile to create a self-contained, untrimmed `win-x64` folder deployment.

Use a noninteractive command and quote every path:

```text
Program/script: C:\Tools\Carrot CLI\carrot-cli.exe
Arguments: server-info --endpoint "http://localhost:8080/service"
Start in: C:\Tools\Carrot CLI
```

The `Start in` value is required so relative configuration resolves consistently. `server-info` is noninteractive and writes its deterministic report to standard output. Configure the task to record the process exit code: `0` is success, `1` is invalid configuration, `4` is an endpoint or `/list` failure, and `130` indicates cancellation. The named process and preview orchestration remains deferred.
