using System.Text.Json;
using Carrot.Cli.CarrotApi;
using Carrot.Cli.Processing;
using Spectre.Console;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>Renders the complete JSON request package for a retained prepared batch.</summary>
/// <remarks>
/// Rendering is local and read-only: it does not contact Carrot or write an artifact. The output
/// includes full extracted content and may therefore be large or sensitive in terminal scrollback.
/// </remarks>
/// <seealso cref="ClusterRequestFactory"/>
/// <seealso cref="PreparedDocumentBatch"/>
internal sealed class PreparedJsonPackageRenderer
{
    #region implementation

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly IAnsiConsole _console;
    private readonly ClusterRequestFactory _requestFactory;

    /**************************************************************/
    /// <summary>Initializes the renderer with its console and shared wire-contract factory.</summary>
    /// <param name="console">The console that receives the JSON preview.</param>
    /// <param name="requestFactory">The shared prepared-document request mapper.</param>
    public PreparedJsonPackageRenderer(IAnsiConsole console, ClusterRequestFactory requestFactory)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(requestFactory);
        _console = console;
        _requestFactory = requestFactory;

        #endregion
    }

    /**************************************************************/
    /// <summary>Displays an indented serialization of the exact package for all ready documents.</summary>
    /// <param name="batch">The retained batch whose successful documents form the request.</param>
    /// <remarks>
    /// <see cref="Text"/> is used instead of markup so brackets and other document characters are
    /// rendered literally. Full content is retained; this preview is not a shortened row preview.
    /// </remarks>
    internal void Render(PreparedDocumentBatch batch)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(batch);
        var request = _requestFactory.Create(batch.Documents);
        var json = JsonSerializer.Serialize(request, SerializerOptions);
        _console.Write(new Panel(new Text(json))
            .Header($"[orange1]JSON Request Package[/] — {batch.Documents.Count:N0} document(s)")
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(Color.Orange1)));
        _console.MarkupLine("[yellow]Preview only:[/] no server request was sent and no file was written.");
        _console.WriteLine();

        #endregion
    }

    #endregion
}
