using System.Text.Json;
using Carrot.Cli.CarrotApi;
using Carrot.Cli.CarrotApi.Contracts;
using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Carrot.Cli.Input;
using Carrot.Cli.Processing;
using Carrot.Cli.Reporting;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>Coordinates interactive request preview preparation, validation, display, and optional persistence.</summary>
/// <remarks>This flow owns no cross-workflow state. It prepares its own input, may call <c>/list</c>, never calls
/// <c>/cluster</c>, and retains the exact displayed request while a save is declined or fails.</remarks>
internal sealed class InteractivePreviewFlow
{
    #region implementation

    private const string DefaultServiceEndpoint = "http://localhost:8080/service";
    private readonly IAnsiConsole _console;
    private readonly InputPathNormalizer _pathNormalizer;
    private readonly InputSourceResolver _inputResolver;
    private readonly IDocumentPreparationWorkflow _preparationWorkflow;
    private readonly EndpointResolver _endpointResolver;
    private readonly ClusteringConfigurationResolver _configurationResolver;
    private readonly ClusteringConfigurationValidator _configurationValidator;
    private readonly ICarrotApiClient _apiClient;
    private readonly ClusterRequestFactory _requestFactory;
    private readonly PreparedJsonPackagePager _pager;
    private readonly IJsonArtifactWriter _jsonWriter;
    private readonly TimeSpan _httpTimeout;

    /**************************************************************/
    /// <summary>Defines the mutually exclusive interactive clustering selection forms.</summary>
    private enum SelectionMode { Direct, Template }

    /**************************************************************/
    /// <summary>Initializes the focused interactive preview flow with shared preparation and validation boundaries.</summary>
    public InteractivePreviewFlow(IAnsiConsole console, InputPathNormalizer pathNormalizer, InputSourceResolver inputResolver,
        IDocumentPreparationWorkflow preparationWorkflow, EndpointResolver endpointResolver,
        ClusteringConfigurationResolver configurationResolver, ClusteringConfigurationValidator configurationValidator,
        ICarrotApiClient apiClient, ClusterRequestFactory requestFactory, PreparedJsonPackagePager pager,
        IJsonArtifactWriter jsonWriter, IOptions<CarrotCliOptions> options)
    {
        ArgumentNullException.ThrowIfNull(console); ArgumentNullException.ThrowIfNull(pathNormalizer);
        ArgumentNullException.ThrowIfNull(inputResolver); ArgumentNullException.ThrowIfNull(preparationWorkflow);
        ArgumentNullException.ThrowIfNull(endpointResolver); ArgumentNullException.ThrowIfNull(configurationResolver);
        ArgumentNullException.ThrowIfNull(configurationValidator); ArgumentNullException.ThrowIfNull(apiClient);
        ArgumentNullException.ThrowIfNull(requestFactory); ArgumentNullException.ThrowIfNull(pager);
        ArgumentNullException.ThrowIfNull(jsonWriter); ArgumentNullException.ThrowIfNull(options);
        _console = console; _pathNormalizer = pathNormalizer; _inputResolver = inputResolver;
        _preparationWorkflow = preparationWorkflow; _endpointResolver = endpointResolver;
        _configurationResolver = configurationResolver; _configurationValidator = configurationValidator;
        _apiClient = apiClient; _requestFactory = requestFactory; _pager = pager; _jsonWriter = jsonWriter;
        _httpTimeout = TimeSpan.FromSeconds(options.Value.HttpTimeoutSeconds);
    }

    /**************************************************************/
    /// <summary>Collects one independent input and displays its validated request without cluster submission.</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var inputText = await new TextPrompt<string>("Preview input path [grey](file, folder, or ZIP)[/]:")
            .PromptStyle("yellow").Validate(validatePath).ShowAsync(_console, cancellationToken).ConfigureAwait(false);
        var inputPath = _pathNormalizer.Normalize(inputText).Value!;
        var recursive = Directory.Exists(inputPath) && await new ConfirmationPrompt("Include subfolders?") { DefaultValue = false }
            .ShowAsync(_console, cancellationToken).ConfigureAwait(false);
        var endpointText = await new TextPrompt<string>("Carrot service endpoint [grey](the value is not persisted)[/]:")
            .DefaultValue(DefaultServiceEndpoint).PromptStyle("yellow").Validate(validateEndpoint)
            .ShowAsync(_console, cancellationToken).ConfigureAwait(false);
        var endpoint = _endpointResolver.Resolve(endpointText).Value!;
        var selection = await collectSelectionAsync(cancellationToken).ConfigureAwait(false);
        _console.MarkupLine("[orange1]Preparing documents and validating Carrot configuration...[/]");
        var prepared = await _preparationWorkflow.PrepareAsync(new PrepareDocumentsRequest { InputPaths = [inputPath], Recursive = recursive }, cancellationToken).ConfigureAwait(false);
        renderMessages(prepared.Messages);
        if (prepared.Value is not { } batch || batch.Documents.Count == 0) { _console.MarkupLine("[red]No successfully prepared documents are available to preview.[/]"); return; }
        var resolved = await _configurationResolver.ResolveAsync(selection, cancellationToken).ConfigureAwait(false);
        if (resolved.Value is not { } configuration) { renderMessages(resolved.Messages); return; }
        var available = await _apiClient.GetConfigurationAsync(endpoint, _httpTimeout, null, cancellationToken).ConfigureAwait(false);
        if (available.Value is null) { renderMessages(available.Messages); return; }
        var validation = _configurationValidator.Validate(configuration, available.Value);
        if (validation.Status == OperationStatus.Failure) { renderMessages(validation.Messages); return; }
        var request = _requestFactory.Create(batch.Documents, configuration);
        await _pager.ShowAsync(
            request,
            batch.Documents.Count,
            token => saveAsync(inputPath, request, token),
            token => saveWorkbenchAsync(inputPath, request, token),
            cancellationToken).ConfigureAwait(false);
    }

    /**************************************************************/
    /// <summary>Collects a direct algorithm/language pair or an advertised template plus optional parameters file.</summary>
    private async Task<ClusteringSelection> collectSelectionAsync(CancellationToken cancellationToken)
    {
        var mode = await new SelectionPrompt<SelectionMode>()
            .Title("[bold orange1]Clustering selection[/]")
            .UseConverter(value => value == SelectionMode.Direct ? "Algorithm and language" : "Server template")
            .AddChoices(SelectionMode.Direct, SelectionMode.Template)
            .ShowAsync(_console, cancellationToken).ConfigureAwait(false);
        var parametersFile = await new TextPrompt<string>("Parameters JSON file [grey](optional)[/]:")
            .AllowEmpty().PromptStyle("yellow").ShowAsync(_console, cancellationToken).ConfigureAwait(false);
        if (mode == SelectionMode.Template)
        {
            var template = await new TextPrompt<string>("Template:").PromptStyle("yellow")
                .ShowAsync(_console, cancellationToken).ConfigureAwait(false);
            return new ClusteringSelection { Template = template, ParametersFile = string.IsNullOrWhiteSpace(parametersFile) ? null : parametersFile };
        }

        var algorithm = await new TextPrompt<string>("Algorithm:").DefaultValue("Lingo").PromptStyle("yellow")
            .ShowAsync(_console, cancellationToken).ConfigureAwait(false);
        var language = await new TextPrompt<string>("Language:").DefaultValue("English").PromptStyle("yellow")
            .ShowAsync(_console, cancellationToken).ConfigureAwait(false);
        return new ClusteringSelection { Algorithm = algorithm, Language = language, ParametersFile = string.IsNullOrWhiteSpace(parametersFile) ? null : parametersFile };
    }

    /**************************************************************/
    /// <summary>Validates one interactive source path through shared normalization and resolver rules.</summary>
    private ValidationResult validatePath(string value)
    {
        var normalized = _pathNormalizer.Normalize(value);
        if (normalized.Value is null) return ValidationResult.Error(normalized.Messages[0].Message);
        var source = _inputResolver.Resolve(normalized.Value);
        return source.Value is null ? ValidationResult.Error(source.Messages[0].Message) : ValidationResult.Success();
    }

    /**************************************************************/
    /// <summary>Validates one interactive endpoint through the shared strict endpoint policy.</summary>
    private ValidationResult validateEndpoint(string value)
    {
        var endpoint = _endpointResolver.Resolve(value);
        return endpoint.Value is null ? ValidationResult.Error(endpoint.Messages[0].Message) : ValidationResult.Success();
    }

    /**************************************************************/
    /// <summary>Explicitly saves the already displayed request and preserves it on declined or failed persistence.</summary>
    private async Task saveAsync(string inputPath, ClusterRequest request, CancellationToken cancellationToken)
    {
        var suggestedPath = Path.Combine(Path.GetDirectoryName(inputPath) ?? Directory.GetCurrentDirectory(), $"{Path.GetFileNameWithoutExtension(inputPath)}.request.json");
        var path = await new TextPrompt<string>("Save request JSON path:").DefaultValue(suggestedPath).PromptStyle("yellow")
            .ShowAsync(_console, cancellationToken).ConfigureAwait(false);
        var normalized = Path.GetFullPath(path.Trim().Trim('\"', '\''));
        var overwrite = !File.Exists(normalized) || await new ConfirmationPrompt("Replace the existing request JSON?") { DefaultValue = false }
            .ShowAsync(_console, cancellationToken).ConfigureAwait(false);
        if (!overwrite) { _console.MarkupLine("[yellow]Save cancelled; the preview remains available.[/]"); return; }
        try { await _jsonWriter.WriteAsync(normalized, request, overwrite: true, cancellationToken).ConfigureAwait(false); _console.MarkupLine($"[green]Saved:[/] {Markup.Escape(normalized)}"); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        { _console.MarkupLine("[red]The request JSON could not be written; the preview remains available.[/]"); }
    }

    /**************************************************************/
    /// <summary>Saves the root document array expected by the Carrot Workbench Local file data source.</summary>
    /// <remarks>The API request wrapper contains algorithm and language settings, while Workbench imports a JSON
    /// array of records. This action deliberately exports only the exact prepared document values.</remarks>
    private async Task saveWorkbenchAsync(string inputPath, ClusterRequest request, CancellationToken cancellationToken)
    {
        var suggestedPath = Path.Combine(Path.GetDirectoryName(inputPath) ?? Directory.GetCurrentDirectory(), $"{Path.GetFileNameWithoutExtension(inputPath)}.workbench.json");
        var path = await new TextPrompt<string>("Save Workbench JSON path:").DefaultValue(suggestedPath).PromptStyle("yellow")
            .ShowAsync(_console, cancellationToken).ConfigureAwait(false);
        var normalized = Path.GetFullPath(path.Trim().Trim('\"', '\''));
        var overwrite = !File.Exists(normalized) || await new ConfirmationPrompt("Replace the existing Workbench JSON?") { DefaultValue = false }
            .ShowAsync(_console, cancellationToken).ConfigureAwait(false);
        if (!overwrite) { _console.MarkupLine("[yellow]Save cancelled; the preview remains available.[/]"); return; }
        try
        {
            await _jsonWriter.WriteAsync(normalized, request.Documents, overwrite: true, cancellationToken).ConfigureAwait(false);
            _console.MarkupLine($"[green]Saved Workbench JSON:[/] {Markup.Escape(normalized)}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            _console.MarkupLine("[red]The Workbench JSON could not be written; the preview remains available.[/]");
        }
    }

    /**************************************************************/
    /// <summary>Renders safe preparation and validation diagnostics.</summary>
    private void renderMessages(IReadOnlyList<OperationMessage> messages)
    {
        foreach (var message in messages) _console.MarkupLine($"[{(message.Severity == OperationMessageSeverity.Error ? "red" : "yellow")}]• {Markup.Escape(message.Message)}[/]");
    }

    #endregion
}
