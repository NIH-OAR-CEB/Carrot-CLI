using System.Globalization;

namespace Carrot.Cli.Reporting;

/**************************************************************/
/// <summary>
/// Creates an editable, immediately valid default destination for an interactive Excel export.
/// </summary>
/// <remarks>
/// The user Documents directory is preferred because it is normally writable and easy to find.
/// The current working directory is used only when an existing Documents directory is unavailable.
/// A local timestamp keeps the proposed filename readable and reduces accidental overwrite prompts.
/// </remarks>
internal sealed class ExcelOutputPathSuggester
{
    #region implementation

    private readonly TimeProvider _timeProvider;

    /**************************************************************/
    /// <summary>Initializes the suggester with the clock used to create readable filenames.</summary>
    /// <param name="timeProvider">The clock supplying the operator's current local time.</param>
    public ExcelOutputPathSuggester(TimeProvider timeProvider)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a complete `.xlsx` path suitable for the interactive prompt default.</summary>
    /// <returns>An absolute path under an existing, operator-friendly directory.</returns>
    internal string Suggest()
    {
        #region implementation

        var documentsDirectory = Environment.GetFolderPath(
            Environment.SpecialFolder.MyDocuments,
            Environment.SpecialFolderOption.DoNotVerify);
        var outputDirectory = !string.IsNullOrWhiteSpace(documentsDirectory)
            && Directory.Exists(documentsDirectory)
                ? documentsDirectory
                : Directory.GetCurrentDirectory();
        var timestamp = _timeProvider
            .GetLocalNow()
            .ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

        return Path.GetFullPath(Path.Combine(outputDirectory, $"carrot-results-{timestamp}.xlsx"));

        #endregion
    }

    #endregion
}
