# Initial Carrot CLI Menus

**Status:** Done

## Summary

Implement the runnable CLI shell while leaving document processing, preview generation, and server communication stubbed. No-argument execution will open an interactive Spectre.Console menu; users can navigate workflow submenus, view Markdown-backed help, view About information, and exit cleanly.

## Interaction Contract

```text
Main Menu
├── Process Documents
│   ├── Execute → "Pending Implementation"
│   ├── Help
│   └── Back
├── Preview Request
│   ├── Execute → "Pending Implementation"
│   ├── Help
│   └── Back
├── Server Information
│   ├── Execute → "Pending Implementation"
│   ├── Help
│   └── Back
├── Help → topic-selection menu
├── About
└── Exit
```

- Keep menus active until the user selects Back or Exit. Pending selections must not resolve or invoke operational workflow/API stubs.
- Main-menu Help opens a topic picker; workflow Help opens its corresponding topic and then returns to that workflow menu.
- Provide topics for getting started, process, preview, server information, commands/options, supported formats, extraction rules, clustering settings, output columns, Task Scheduler, exit codes, privacy, and troubleshooting.
- `carrot-cli help` displays Getting Started; `help <topic>` resolves case-insensitive topic keys and aliases. Unknown topics display the valid list and return exit code `1`.
- `carrot-cli about`, `--help`, and `--version` work. About shows the assembly version, supported formats, Carrot 4.8.6 compatibility, and the third-party-notices location.
- Exit returns `0`; cancellation returns `130`. No-argument execution in a noninteractive console prints usage guidance and returns `1`.

## Implementation Changes

- Complete the composition root, Spectre DI adapter, resolver ownership/disposal, command factory, and default `InteractiveCommand`. Change `CommandAppFactory.Create` to accept the mutable `IServiceCollection` so Spectre can register command types before the provider is built.
- Register only the stateless UI/help services needed for this milestone. Keep `ProcessCommand`, `PreviewCommand`, `ServerInfoCommand`, and all operational services unchanged and deferred.
- Refactor `InteractiveMenu` to depend on `IAnsiConsole`, `HelpRenderer`, and `AboutRenderer`, removing workflow/reporter dependencies that would instantiate stubs. Represent menu choices with internal identifiers rather than dispatching by display text.
- Add an internal help-topic catalog, embedded-resource content provider, and lightweight Markdown renderer. Store canonical runtime content as one file per topic under `src/Carrot.Cli/Docs/` and embed those files in `carrot-cli.dll`, eliminating working-directory and deployment-file dependencies.
- Support ATX headings, paragraphs, ordered/unordered lists, fenced code blocks, inline code, emphasis, and links. Escape all content before translating it to Spectre markup; unsupported syntax falls back to escaped plain text.
- Change `HelpRenderer.Render` to report whether a topic was resolved so `HelpCommand` can return `0` or `1`. Missing embedded resources produce a concise error rather than an exception trace.
- Update README, CLI reference, and troubleshooting documentation to describe the now-runnable UI shell, menu hierarchy, topic keys, and `Pending Implementation` behavior. Preserve the completed scaffold plan as historical documentation.
- Apply the repository's verbose C# documentation conventions to every new or touched type/member, while removing obsolete “always throws” documentation from implemented UI methods.

## Internal Interfaces and Types

- No new public API is introduced.
- Add internal `HelpTopic`, `HelpTopicCatalog`, and `IHelpContentProvider` contracts plus the Markdown renderer.
- Update the internal signatures and constructors of `CommandAppFactory`, `InteractiveMenu`, `HelpRenderer`, `AboutRenderer`, `InteractiveCommand`, `HelpCommand`, and `AboutCommand` to support the runnable UI and constructor injection.

## Test Plan

- Add centrally pinned `Spectre.Console.Cli.Testing` `0.55.0` to the test project.
- Use `CommandAppTester`/`TestConsole` to verify no arguments open the main menu, every workflow screen contains Help and Back, every Execute choice writes the exact text `Pending Implementation`, and Exit/cancellation return the documented codes.
- Verify workflow Help returns to its originating menu and the Help picker can render every catalog topic and return to Main.
- Test `help`, topic aliases, unknown topics, `about`, `--help`, and `--version` as black-box command routes.
- Unit-test resource lookup and Markdown rendering, including headings, lists, fenced code, links, Spectre-markup escaping, unsupported syntax, and missing resources.
- Add a publish smoke test that runs embedded help from outside the repository working directory.
- Leave broad operational acceptance tests skipped; run the complete existing suite plus the new active CLI UI tests.

## Assumptions

- This milestone does not collect process/preview/server settings; those setup prompts belong to a later operational milestone.
- Named `process`, `preview`, and `server-info` execution remains deferred, while their generated Spectre help metadata remains available.
- Embedded Markdown is authoritative for runtime help and requires rebuilding to change.
- The DI bridge and test approach follow Spectre's official [dependency-injection](https://spectreconsole.net/cli/tutorials/dependency-injection-in-cli-apps/) and [CLI testing](https://spectreconsole.net/cli/how-to/testing-command-line-applications/) guidance; the selected testing package is available at the solution's pinned [0.55.0 version](https://www.nuget.org/packages/Spectre.Console.Cli.Testing/0.55.0).

## Completion Evidence

Completed on 2026-08-28. No-argument execution now resolves the interactive main menu without constructing operational services. Process Documents, Preview Request, and Server Information provide Execute, Help, and Back selections; Execute displays the exact text `Pending Implementation`. The main Help menu and named `help` command render 13 embedded Markdown topics, while About and generated Help/Version routes are active.

`dotnet build .\Carrot-CLI.slnx --no-restore --disable-build-servers -m:1 --verbosity minimal` completed with 0 warnings and 0 errors. `dotnet test .\Carrot-CLI.slnx --no-build --no-restore --verbosity normal` passed 37 active tests, intentionally skipped 17 deferred operational tests, and failed 0 of 54 total tests. The self-contained `win-x64` publish completed, and its executable rendered embedded Getting Started help with exit code `0` while running from `C:\Source\Programs`. `dotnet format .\Carrot-CLI.slnx --verify-no-changes --no-restore` and `git diff --check` completed without source or whitespace errors.
