using System.Reflection;
using Spectre.Console;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>
/// Renders application version, supported formats, Carrot compatibility, and notices.
/// </summary>
internal sealed class AboutRenderer
{
    #region implementation

    private readonly IAnsiConsole _console;

    /**************************************************************/
    /// <summary>
    /// Initializes the renderer with the console receiving application information.
    /// </summary>
    /// <param name="console">The injectable Spectre console.</param>
    public AboutRenderer(IAnsiConsole console)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(console);
        _console = console;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Displays the about panel and references to third-party notices.
    /// </summary>
    internal void Render()
    {
        #region implementation

        var table = new Table()
            .Border(TableBorder.None)
            .HideHeaders();
        table.AddColumn(new TableColumn(string.Empty));
        table.AddColumn(new TableColumn(string.Empty));
        table.AddRow(new Markup("[bold]Version[/]"), new Text(getApplicationVersion()));
        table.AddRow(new Markup("[bold]Compatibility[/]"), new Text("Carrot 4.8.6 Document Clustering Server"));
        table.AddRow(new Markup("[bold]Formats[/]"), new Text(".docx, .xlsx, .pptx, .txt, .md, .pdf"));
        table.AddRow(new Markup("[bold]Notices[/]"), new Text("THIRD-PARTY-NOTICES.md"));

        _console.Write(new Panel(table)
            .Header("[bold orange1]Carrot CLI[/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(Color.Grey)));
        _console.WriteLine();

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Reads a concise informational version for the About panel.
    /// </summary>
    /// <returns>The informational version without build metadata when available.</returns>
    private static string getApplicationVersion()
    {
        #region implementation

        var assembly = typeof(Program).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion.Split('+', 2)[0];
        }

        return assembly.GetName().Version?.ToString() ?? "0.0.0";

        #endregion
    }

    #endregion
}
