# Interactive Server Information

Status: Pending

Category: Interactive parity

Depends on: 05 Named Server Information

## 1. Outcome

Replace the main-menu **Server Information** `Pending Implementation` panel with an interactive endpoint prompt and reusable `/list` results view.

## 2. Problem

The interactive workflow submenu still routes Execute to a generic pending panel even though the API client can retrieve `/list` and the named Server Information plan will establish shared resolution and presentation behavior.

## 3. Solution Vision

Add a focused `ServerInformationFlow` that prompts with the existing localhost endpoint default, validates it without persisting, calls the shared server-information service, and renders a pageable/readable configuration view. `InteractiveMenu` will own navigation only.

## 4. Scope

- Endpoint collection, validation, loading feedback, safe failures, cancellation, and return-to-menu behavior.
- Algorithms, compatible languages, and templates presentation using shared server-information application logic.
- Replacement of only the Server Information pending route.

## 5. Non-goals

- Selecting settings for processing, saving server profiles, writing files, or changing named command output.
- Implementing Preview Request.

## 6. Technical Approach

- Reuse `EndpointResolver`, `ICarrotApiClient`, and the shared server-information model rather than invoking `ServerInfoCommand` from the menu.
- Keep the default endpoint editable and nonpersistent.
- Adapt presentation to terminal height if `/list` content exceeds the available screen.

## 7. Implementation Steps

1. Extract/reuse a server-information application service from named command orchestration.
2. Add the interactive flow and terminal-sized results renderer.
3. Replace the generic Server Information workflow branch in `InteractiveMenu`.
4. Add prompt, success, paging, failure, Escape, cancellation, and no-file/no-cluster tests.
5. Update README, preamble, CLI reference, getting-started, server-info, and troubleshooting documentation.

## 8. Acceptance Criteria

- Execute prompts for a valid `/service` endpoint and displays exact `/list` algorithms, languages, and templates.
- The flow does not call `/cluster`, write files, or persist the endpoint.
- Escape/cancellation and safe failures return to the owning menu consistently.
- Every changed production method is covered; build, tests, formatting, publish, and whitespace verification pass.

## 9. Risks and Assumptions

- `/list` responses can be large, so presentation must avoid dumping content beyond terminal height.

## 10. Deferred Follow-up

- Interactive Preview Request and clustering-setting selection.

