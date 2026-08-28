using Carrot.Cli.Common;
using Carrot.Cli.Input;
using Carrot.Cli.Processing;
using Spectre.Console;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>Coordinates editable path collection, preparation, review, and retained batch actions.</summary>
/// <remarks>
/// This menu intentionally stops at the process boundary. The pending process action reports the
/// count of exact prepared documents but does not resolve HTTP, clustering, or reporting services.
/// The JSON preview serializes the future request locally without sending or persisting it.
/// </remarks>
internal sealed class ProcessDocumentsMenu
{
    #region implementation

    private readonly IAnsiConsole _console;
    private readonly HelpRenderer _helpRenderer;
    private readonly InputPathNormalizer _pathNormalizer;
    private readonly InputSourceResolver _inputResolver;
    private readonly IDocumentPreparationWorkflow _preparationWorkflow;
    private readonly PreparedResultsPager _pager;
    private readonly PreparedJsonPackagePager _jsonPackagePager;

    /**************************************************************/
    /// <summary>Defines actions available while assembling an ordered input-path list.</summary>
    private enum SetupChoice
    {
        /**************************************************************/
        /// <summary>Adds one quoted or unquoted path.</summary>
        AddPath,

        /**************************************************************/
        /// <summary>Removes one previously added path.</summary>
        RemovePath,

        /**************************************************************/
        /// <summary>Discovers and extracts the queued inputs.</summary>
        Prepare,

        /**************************************************************/
        /// <summary>Displays Process Documents help.</summary>
        Help,

        /**************************************************************/
        /// <summary>Returns to the application main menu.</summary>
        Back
    }

    /**************************************************************/
    /// <summary>Defines actions available for one retained prepared batch.</summary>
    private enum BatchChoice
    {
        /**************************************************************/
        /// <summary>Reopens the prepared-results pager.</summary>
        ViewResults,

        /**************************************************************/
        /// <summary>Displays the complete JSON request package for ready documents.</summary>
        PreviewJson,

        /**************************************************************/
        /// <summary>Explains why JSON preview is unavailable when no rows are ready.</summary>
        PreviewJsonUnavailable,

        /**************************************************************/
        /// <summary>Displays the pending process handoff for ready documents.</summary>
        Process,

        /**************************************************************/
        /// <summary>Explains why processing is unavailable when no rows are ready.</summary>
        ProcessUnavailable,

        /**************************************************************/
        /// <summary>Discards the retained batch and returns to path setup.</summary>
        StartOver,

        /**************************************************************/
        /// <summary>Displays Process Documents help.</summary>
        Help,

        /**************************************************************/
        /// <summary>Discards the retained batch and returns to the main menu.</summary>
        Back
    }

    /**************************************************************/
    /// <summary>Defines the owning menu's response to completion of retained batch actions.</summary>
    private enum BatchDisposition
    {
        /**************************************************************/
        /// <summary>Returns to editable path setup.</summary>
        StartOver,

        /**************************************************************/
        /// <summary>Returns to the application main menu.</summary>
        Back
    }

    /**************************************************************/
    /// <summary>Initializes Process Documents with presentation, validation, and preparation collaborators.</summary>
    public ProcessDocumentsMenu(
        IAnsiConsole console,
        HelpRenderer helpRenderer,
        InputPathNormalizer pathNormalizer,
        InputSourceResolver inputResolver,
        IDocumentPreparationWorkflow preparationWorkflow,
        PreparedResultsPager pager,
        PreparedJsonPackagePager jsonPackagePager)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(helpRenderer);
        ArgumentNullException.ThrowIfNull(pathNormalizer);
        ArgumentNullException.ThrowIfNull(inputResolver);
        ArgumentNullException.ThrowIfNull(preparationWorkflow);
        ArgumentNullException.ThrowIfNull(pager);
        ArgumentNullException.ThrowIfNull(jsonPackagePager);
        _console = console;
        _helpRenderer = helpRenderer;
        _pathNormalizer = pathNormalizer;
        _inputResolver = inputResolver;
        _preparationWorkflow = preparationWorkflow;
        _pager = pager;
        _jsonPackagePager = jsonPackagePager;

        #endregion
    }

    /**************************************************************/
    /// <summary>Runs path setup and retained-batch actions until the operator returns to Main.</summary>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task representing Process Documents navigation.</returns>
    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        #region implementation

        var inputPaths = new List<string>();
        while (true)
        {
            renderInputPaths(inputPaths);
            var setupChoice = await createSetupPrompt(inputPaths.Count > 0)
                .ShowAsync(_console, cancellationToken)
                .ConfigureAwait(false);

            switch (setupChoice)
            {
                case SetupChoice.AddPath:
                    await addPathAsync(inputPaths, cancellationToken).ConfigureAwait(false);
                    break;
                case SetupChoice.RemovePath:
                    await removePathAsync(inputPaths, cancellationToken).ConfigureAwait(false);
                    break;
                case SetupChoice.Prepare:
                    {
                        var recursive = inputPaths.Any(Directory.Exists)
                            && await new ConfirmationPrompt("Include subfolders for all folder inputs?")
                            { DefaultValue = false }
                                .ShowAsync(_console, cancellationToken)
                                .ConfigureAwait(false);

                        _console.MarkupLine("[orange1]Preparing documents…[/]");
                        var result = await _preparationWorkflow.PrepareAsync(
                            new PrepareDocumentsRequest
                            {
                                InputPaths = inputPaths.AsReadOnly(),
                                Recursive = recursive
                            },
                            cancellationToken).ConfigureAwait(false);
                        renderPreparationSummary(result);

                        if (result.Value is not null)
                        {
                            await _pager.ShowAsync(result, cancellationToken).ConfigureAwait(false);
                            var disposition = await runBatchActionsAsync(result, cancellationToken).ConfigureAwait(false);
                            if (disposition == BatchDisposition.Back)
                            {
                                return;
                            }

                            inputPaths.Clear();
                        }

                        break;
                    }
                case SetupChoice.Help:
                    _helpRenderer.Render("process");
                    break;
                case SetupChoice.Back:
                    return;
                default:
                    throw new InvalidOperationException($"Unsupported document setup action: {setupChoice}");
            }
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates the setup prompt and omits actions that require at least one queued path.</summary>
    private static SelectionPrompt<SetupChoice> createSetupPrompt(bool hasInputPaths)
    {
        #region implementation

        var choices = new List<SetupChoice> { SetupChoice.AddPath };
        if (hasInputPaths)
        {
            choices.Add(SetupChoice.RemovePath);
            choices.Add(SetupChoice.Prepare);
        }

        choices.Add(SetupChoice.Help);
        choices.Add(SetupChoice.Back);
        return new SelectionPrompt<SetupChoice>()
            .Title("[bold orange1]Process Documents[/] — Build an input batch")
            .HighlightStyle(new Style(Color.Black, Color.Orange1))
            .UseConverter(choice => choice switch
            {
                SetupChoice.AddPath => "Add Path",
                SetupChoice.RemovePath => "Remove Path",
                SetupChoice.Prepare => "Prepare Documents",
                SetupChoice.Help => "Help",
                SetupChoice.Back => "Back to Main Menu",
                _ => choice.ToString()
            })
            .AddChoices(choices)
            .AddCancelResult(SetupChoice.Back);

        #endregion
    }

    /**************************************************************/
    /// <summary>Prompts for and validates one quoted or unquoted input path.</summary>
    private async Task addPathAsync(List<string> inputPaths, CancellationToken cancellationToken)
    {
        #region implementation

        var rawPath = await new TextPrompt<string>(
                "Enter a file, folder, or ZIP path [grey](surrounding quotes are accepted)[/]:")
            .PromptStyle("yellow")
            .Validate(value => validatePath(value))
            .ShowAsync(_console, cancellationToken)
            .ConfigureAwait(false);
        var normalizedPath = _pathNormalizer.Normalize(rawPath).Value!;

        if (inputPaths.Contains(normalizedPath, StringComparer.OrdinalIgnoreCase))
        {
            _console.MarkupLine($"[yellow]Path is already queued:[/] {Markup.Escape(normalizedPath)}");
            return;
        }

        inputPaths.Add(normalizedPath);
        _console.MarkupLine($"[green]Added:[/] {Markup.Escape(normalizedPath)}");

        #endregion
    }

    /**************************************************************/
    /// <summary>Validates prompt text through shared normalization and input-strategy resolution.</summary>
    private ValidationResult validatePath(string value)
    {
        #region implementation

        var normalizedPath = _pathNormalizer.Normalize(value);
        if (normalizedPath.Value is null)
        {
            return ValidationResult.Error(normalizedPath.Messages.First().Message);
        }

        var inputSource = _inputResolver.Resolve(normalizedPath.Value);
        return inputSource.Value is null
            ? ValidationResult.Error(inputSource.Messages.First().Message)
            : ValidationResult.Success();

        #endregion
    }

    /**************************************************************/
    /// <summary>Removes one selected path while letting Escape cancel removal.</summary>
    private async Task removePathAsync(List<string> inputPaths, CancellationToken cancellationToken)
    {
        #region implementation

        if (inputPaths.Count == 0)
        {
            return;
        }

        const string cancelChoice = "\0";
        var choices = inputPaths.Append(cancelChoice).ToArray();
        var selected = await new SelectionPrompt<string>()
            .Title("[bold orange1]Remove Path[/]")
            .HighlightStyle(new Style(Color.Black, Color.Orange1))
            .UseConverter(path => path == cancelChoice ? "Cancel" : Markup.Escape(path))
            .AddChoices(choices)
            .AddCancelResult(cancelChoice)
            .ShowAsync(_console, cancellationToken)
            .ConfigureAwait(false);

        if (selected != cancelChoice)
        {
            inputPaths.Remove(selected);
            _console.MarkupLine($"[yellow]Removed:[/] {Markup.Escape(selected)}");
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Runs retained batch actions until the user starts over or confirms return to Main.</summary>
    private async Task<BatchDisposition> runBatchActionsAsync(
        OperationResult<PreparedDocumentBatch> result,
        CancellationToken cancellationToken)
    {
        #region implementation

        var batch = result.Value!;
        while (true)
        {
            var processChoice = batch.Documents.Count > 0
                ? BatchChoice.Process
                : BatchChoice.ProcessUnavailable;
            var previewChoice = batch.Documents.Count > 0
                ? BatchChoice.PreviewJson
                : BatchChoice.PreviewJsonUnavailable;
            var selected = await new SelectionPrompt<BatchChoice>()
                .Title("[bold orange1]Prepared Batch Actions[/]")
                .HighlightStyle(new Style(Color.Black, Color.Orange1))
                .UseConverter(choice => choice switch
                {
                    BatchChoice.ViewResults => "View Prepared Results",
                    BatchChoice.PreviewJson => $"Preview JSON Package ({batch.Documents.Count:N0} document(s))",
                    BatchChoice.PreviewJsonUnavailable => "Preview JSON Package (unavailable — 0 ready)",
                    BatchChoice.Process => $"Process Prepared Items ({batch.Documents.Count:N0} ready)",
                    BatchChoice.ProcessUnavailable => "Process Prepared Items (unavailable — 0 ready)",
                    BatchChoice.StartOver => "Start Over",
                    BatchChoice.Help => "Help",
                    BatchChoice.Back => "Back to Main Menu",
                    _ => choice.ToString()
                })
                .AddChoices(
                    BatchChoice.ViewResults,
                    previewChoice,
                    processChoice,
                    BatchChoice.StartOver,
                    BatchChoice.Help,
                    BatchChoice.Back)
                .AddCancelResult(BatchChoice.Back)
                .ShowAsync(_console, cancellationToken)
                .ConfigureAwait(false);

            switch (selected)
            {
                case BatchChoice.ViewResults:
                    await _pager.ShowAsync(result, cancellationToken).ConfigureAwait(false);
                    break;
                case BatchChoice.PreviewJson:
                    await _jsonPackagePager.ShowAsync(batch, cancellationToken).ConfigureAwait(false);
                    break;
                case BatchChoice.PreviewJsonUnavailable:
                    _console.MarkupLine("[yellow]No successfully prepared documents are available to preview.[/]");
                    break;
                case BatchChoice.Process:
                    renderPendingProcess(batch.Documents.Count);
                    break;
                case BatchChoice.ProcessUnavailable:
                    _console.MarkupLine("[yellow]No successfully prepared documents are available to process.[/]");
                    break;
                case BatchChoice.StartOver:
                    if (await confirmDiscardAsync("Discard this prepared batch and start over?", cancellationToken)
                        .ConfigureAwait(false))
                    {
                        return BatchDisposition.StartOver;
                    }

                    break;
                case BatchChoice.Help:
                    _helpRenderer.Render("process");
                    break;
                case BatchChoice.Back:
                    if (await confirmDiscardAsync("Discard this prepared batch and return to Main Menu?", cancellationToken)
                        .ConfigureAwait(false))
                    {
                        return BatchDisposition.Back;
                    }

                    break;
                default:
                    throw new InvalidOperationException($"Unsupported prepared-batch action: {selected}");
            }
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Prompts before discarding a retained in-memory batch.</summary>
    private Task<bool> confirmDiscardAsync(string prompt, CancellationToken cancellationToken)
    {
        #region implementation

        return new ConfirmationPrompt(prompt) { DefaultValue = false }
            .ShowAsync(_console, cancellationToken);

        #endregion
    }

    /**************************************************************/
    /// <summary>Displays the exact queued input order above setup actions.</summary>
    private void renderInputPaths(IReadOnlyList<string> inputPaths)
    {
        #region implementation

        if (inputPaths.Count == 0)
        {
            _console.MarkupLine("[grey]No input paths have been added.[/]");
            return;
        }

        var table = new Table().Border(TableBorder.Simple).AddColumn("#").AddColumn("Queued Input Path");
        for (var index = 0; index < inputPaths.Count; index++)
        {
            table.AddRow(new Text((index + 1).ToString()), new Text(inputPaths[index]));
        }

        _console.Write(table);

        #endregion
    }

    /**************************************************************/
    /// <summary>Displays aggregate preparation status and all batch-level diagnostics.</summary>
    private void renderPreparationSummary(OperationResult<PreparedDocumentBatch> result)
    {
        #region implementation

        var statusMarkup = result.Status switch
        {
            OperationStatus.Success => "[green]Success[/]",
            OperationStatus.PartialSuccess => "[yellow]Partial Success[/]",
            OperationStatus.Failure => "[red]Failure[/]",
            _ => Markup.Escape(result.Status.ToString())
        };
        var readyCount = result.Value?.Documents.Count ?? 0;
        var rowCount = result.Value?.Rows.Count ?? 0;
        _console.MarkupLine(
            $"[bold]Preparation:[/] {statusMarkup}  [green]Ready: {readyCount:N0}[/]  [grey]Rows: {rowCount:N0}[/]");

        foreach (var message in result.Messages)
        {
            var color = message.Severity switch
            {
                OperationMessageSeverity.Information => "grey",
                OperationMessageSeverity.Warning => "yellow",
                OperationMessageSeverity.Error => "red",
                _ => "white"
            };
            _console.MarkupLine($"[{color}]• {Markup.Escape(message.Message)}[/]");
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Displays a no-side-effect process handoff while retaining the reviewed batch.</summary>
    private void renderPendingProcess(int readyCount)
    {
        #region implementation

        _console.Write(new Panel(
                new Text($"{readyCount:N0} prepared document(s) are ready. Carrot submission and report generation remain pending implementation."))
            .Header("[orange1]Process Prepared Items[/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(Color.Yellow)));
        _console.WriteLine();

        #endregion
    }

    #endregion
}
