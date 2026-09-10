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

        return Suggest("carrot-results");

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a complete `.xlsx` path using the supplied safe filename stem.</summary>
    /// <param name="fileNameStem">The nonempty filename stem without an extension.</param>
    /// <returns>An absolute path under an existing, operator-friendly directory.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="fileNameStem"/> is empty.</exception>
    internal string Suggest(string fileNameStem)
    {
        #region implementation

        ArgumentException.ThrowIfNullOrWhiteSpace(fileNameStem);
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

        return Path.GetFullPath(Path.Combine(outputDirectory, $"{fileNameStem}-{timestamp}.xlsx"));

        #endregion
    }

    #endregion
}
