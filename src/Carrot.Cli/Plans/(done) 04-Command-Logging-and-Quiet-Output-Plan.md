# Command Logging and Quiet Output

Status: Complete

Category: Artifacts and observability

Depends on: 01 Named Command Foundation

## 1. Outcome

Implement deterministic unattended diagnostics for `--log-file` and complete the `--quiet` output contract without changing interactive menu logging.

## 2. Problem

`ProcessSettings` exposes `--log-file` and `--quiet`, and Task Scheduler guidance recommends a log file, but no command-scoped file writer consumes the path. The existing `ILogger<CarrotApiClient>` is host diagnostics rather than the promised operator-selected run log.

## 3. Solution Vision

Add a command-run reporting boundary that writes timestamped, safe UTF-8 diagnostic records to an explicit file while `ConsoleReporter` controls terminal verbosity. Operational commands will report through this shared boundary so scheduled runs retain failures even when normal console output is suppressed.

## 4. Scope

- Validate and normalize an optional log path under an existing parent directory.
- Append safe run lifecycle, warnings, errors, exit code, and artifact paths to UTF-8 logs.
- Define quiet-mode behavior: suppress normal progress/summary output, never suppress warnings or errors.
- Protect concurrent writes within one process and propagate cancellation appropriately.

## 5. Non-goals

- Global rolling logs, retention policies, structured JSON logs, Windows Event Log, or remote sinks.
- Logging extracted document text, request/response bodies, credentials, or server stack traces.
- Making any operational command executable by itself.

## 6. Technical Approach

- Introduce an `ICommandRunLogger` abstraction owned by named orchestration rather than dynamically mutating the Generic Host provider graph.
- Use append-only UTF-8 text with invariant timestamps and one record per line.
- Serialize writes for a run and keep console formatting separate from plain log text.

## 7. Implementation Steps

1. Add log-path resolution and the command-run logger contract/implementation.
2. Extend the named run result/reporting flow to emit safe lifecycle and terminal records.
3. Complete `ConsoleReporter` quiet-mode tests and redirected-console coverage.
4. Register logging services without affecting interactive processing or default host diagnostics.
5. Update Task Scheduler, privacy, troubleshooting, and commands/options documentation.

## 8. Acceptance Criteria

- An explicit valid log path receives append-only UTF-8 records for completion, warning, failure, cancellation, exit code, and artifact paths.
- Missing/invalid log parents fail configuration before processing.
- Quiet mode suppresses informational console output but retains warnings/errors and the requested file log.
- Logs contain no extracted content, API payloads, credentials, or server stack traces.
- All changed production methods are covered; build, tests, formatting, and whitespace verification pass.

## 9. Risks and Assumptions

- Multiple scheduled processes may target one file; append behavior must avoid truncation, but cross-process ordering is not guaranteed.
- Operators remain responsible for log retention and filesystem permissions.

## 10. Deferred Follow-up

- Integration of the logger into the named preview and process command plans.

## 11. Completion Evidence

- Added the singleton `ICommandRunLogger` / `CommandRunLogger` boundary, with normalized optional paths, append-only UTF-8 records, UTC invariant timestamps, one-record-per-line sanitization, in-process serialization, and explicit cancellation logging.
- Registered the logger without changing interactive menus or activating deferred named process/preview command execution.
- Expanded console-reporter coverage across interactive and redirected console capabilities, and added file-logger coverage for complete safe records, append behavior, UTF-8, cancellation, no-op omitted paths, concurrency, and null/invalid contracts.
- Updated Task Scheduler, privacy, troubleshooting, and command-option documentation to state parent-directory, retention, quiet-output, and safe-data boundaries.
