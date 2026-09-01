# Named Server Information

Status: Complete

Category: Operational commands

Depends on: 01 Named Command Foundation

## 1. Outcome

Make `carrot-cli server-info` a functional, noninteractive command that resolves an endpoint, calls `/list`, displays algorithms, languages, and templates, and returns a stable exit code.

## 2. Problem

`ServerInfoCommand` is registered in generated help, but both its constructor and `ExecuteAsync` throw layout-stub exceptions. The API client and `/list` response contract are implemented, so the missing work is command orchestration and presentation.

## 3. Solution Vision

Use the shared endpoint resolver and reporter from the foundation plan. The command will never prompt: it will resolve configuration, call `ICarrotApiClient.GetConfigurationAsync`, render deterministic output, and map failures or cancellation to documented exit codes.

## 4. Scope

- Implement constructor injection and `ExecuteAsync`.
- Resolve `--endpoint`, `CARROTCLI_ENDPOINT`, and `--timeout-seconds`.
- Render exact algorithm/language/template identifiers and safe failures.
- Register and test the complete named command graph.

## 5. Non-goals

- Interactive Server Information.
- Selecting clustering settings or contacting `/cluster`.
- Writing JSON, Excel, or log files.

## 6. Technical Approach

- Delegate URI policy to `RunSettingsResolver`/`EndpointResolver` and HTTP policy to `ICarrotApiClient`.
- Sort only presentation keys when deterministic output requires it; do not mutate or reinterpret server data.
- Return `0` on success, `1` for invalid configuration, `4` for endpoint or `/list` failures, and `130` for cancellation.

## 7. Implementation Steps

1. Implement and document `ServerInfoCommand` constructor and execution.
2. Add production DI resolution for the command and its shared services.
3. Add command-harness tests for option/environment precedence, success output, HTTP failure, malformed response, redirected output, and cancellation.
4. Update CLI reference, exit codes, Task Scheduler notes, server-info help, and troubleshooting status.

## 8. Acceptance Criteria

- `carrot-cli server-info --endpoint <service>` calls only `/list` and prints advertised algorithms, languages, and templates.
- The command never prompts and behaves correctly with redirected input/output.
- Configuration, endpoint, response, and cancellation outcomes return stable documented exit codes.
- Every changed production method is covered; build, tests, formatting, publish, and whitespace verification pass.

## 9. Risks and Assumptions

- `/list` dictionaries may be large or differently ordered across servers; output must remain readable and deterministic.
- This plan assumes the local Carrot 4.8.6 contract remains authoritative.

## 10. Deferred Follow-up

- Interactive Server Information and clustering selection UI.
