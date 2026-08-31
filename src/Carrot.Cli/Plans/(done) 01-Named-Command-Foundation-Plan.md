# Named Command Foundation

Status: Complete

Category: Automation foundation

## 1. Outcome

Implement and test the shared configuration-resolution and terminal-reporting services required by the deferred `process`, `preview`, and `server-info` commands without making any operational command executable in this phase.

## 2. Problem

`RunSettingsResolver` and `ConsoleReporter` still contain five `NotImplementedException("Layout stub only.")` methods. The command option models and exit-code documentation exist, but there is no common implementation for endpoint precedence, normalized run requests, validation messages, quiet output, run summaries, or `/list` presentation.

## 3. Solution Vision

Use `RunSettingsResolver` as the only translation boundary from Spectre settings to validated application requests and `ConsoleReporter` as the only named-command presentation boundary. These services will be independently usable and registered in production DI before command bodies are activated by later plans.

## 4. Scope

- Resolve command options and `CARROTCLI_ENDPOINT` precedence into `ProcessRequest`, `PreviewRequest`, and endpoint values.
- Apply shared defaults, path normalization, timeout validation, and structured configuration failures.
- Render run messages, artifact paths, server configuration, and quiet-mode behavior without exposing server stack traces.
- Register the implemented services and add direct automated coverage for every changed public or internal production method.

## 5. Non-goals

- Executing `process`, `preview`, or `server-info`.
- Parsing parameter-file contents, writing artifacts, or configuring file logging.
- Changing the working interactive Process Documents workflow.

## 6. Technical Approach

- Inject validated `CarrotCliOptions`, `EndpointResolver`, and focused path resolvers rather than reading global state throughout commands.
- Use precedence `--endpoint`, then `CARROTCLI_ENDPOINT`, then a structured missing-endpoint failure for named commands.
- Keep expected input/configuration errors in `OperationResult`; reserve exceptions for programmer faults and cancellation.
- Keep reporter output deterministic and safe for redirected consoles.

## 7. Implementation Steps

1. Implement all three `RunSettingsResolver` methods and update their constructor dependencies and XML documentation.
2. Implement `ConsoleReporter.WriteRunResult` and `WriteServerInfo`, including quiet-mode and redirected-output behavior.
3. Register the services in `ServiceRegistration.AddCarrotCli` without registering the still-deferred end-to-end workflow.
4. Replace the three deferred `CommandContractTests` placeholders with focused resolver/reporter tests where applicable, leaving command execution acceptance for later plans.
5. Update commands/options and exit-code documentation only where implemented precedence or validation differs from current text.

## 8. Acceptance Criteria

- Explicit endpoint values override `CARROTCLI_ENDPOINT`; a missing endpoint produces exit-code-ready configuration messages.
- Process and preview settings normalize documented options into immutable requests without prompting.
- Invalid paths, timeouts, and option combinations return safe structured failures.
- Quiet mode suppresses normal information but not warnings or errors.
- Server and run summaries are deterministic in interactive and redirected consoles.
- Every changed production method has automated coverage; build, tests, formatting, and whitespace verification pass.

## 9. Risks and Assumptions

- Parameter JSON semantics belong to the clustering-configuration plan and should only be path-validated here.
- Output-path rules differ between preview JSON and Excel, so one generic output validator may be inappropriate.
- Named commands must never prompt, even when required configuration is missing.

## 10. Deferred Follow-up

- JSON artifact persistence, clustering selection, file logging, and the three operational command plans.

## 11. Completion Evidence

- `RunSettingsResolver` now resolves explicit/environment endpoints, validated configured defaults, supported normalized inputs, positive timeouts, output/log/parameter paths, and path-role conflicts into immutable process and preview requests.
- `ConsoleReporter` now renders literal deterministic run and server summaries, suppresses informational output in quiet mode, and always preserves warnings and errors.
- Both services are registered in `ServiceRegistration.AddCarrotCli`; the deferred named command and workflow bodies remain unchanged.
- The three layout-only command-contract placeholders were replaced with focused resolver, reporter, redirected-console, null-contract, validation, and production-DI tests.
- The existing command/options and exit-code documentation already described the implemented precedence and deferred command status accurately, so no user-facing behavior text required revision.
- Release verification completed with 153 passing tests, 7 intentionally skipped deferred tests, and 0 failures out of 160 total. Release build completed with 0 warnings and 0 errors. `dotnet format --verify-no-changes` and `git diff --check` passed.
