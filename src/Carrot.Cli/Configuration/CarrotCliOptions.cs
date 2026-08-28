namespace Carrot.Cli.Configuration;

/**************************************************************/
/// <summary>
/// Defines strongly typed safeguards and defaults loaded from the CarrotCli configuration section.
/// </summary>
/// <seealso cref="CarrotCliOptionsValidator"/>
internal sealed class CarrotCliOptions
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets or sets the maximum number of files or archive entries accepted per run.</summary>
    public int MaximumInputFiles { get; set; } = 10_000;

    /**************************************************************/
    /// <summary>Gets or sets the maximum uncompressed size accepted for one source file.</summary>
    public long MaximumFileSizeBytes { get; set; } = 100L * 1024L * 1024L;

    /**************************************************************/
    /// <summary>Gets or sets the maximum aggregate expanded size accepted for a ZIP archive.</summary>
    public long MaximumExpandedArchiveBytes { get; set; } = 1024L * 1024L * 1024L;

    /**************************************************************/
    /// <summary>Gets or sets the maximum extracted character count accepted across a run.</summary>
    public long MaximumTotalExtractedCharacters { get; set; } = 50_000_000L;

    /**************************************************************/
    /// <summary>Gets or sets the maximum permitted uncompressed-to-compressed ZIP ratio.</summary>
    public double MaximumCompressionRatio { get; set; } = 100D;

    /**************************************************************/
    /// <summary>Gets or sets the maximum number of concurrent document extraction workers.</summary>
    public int ExtractionWorkerCount { get; set; } = 4;

    /**************************************************************/
    /// <summary>Gets or sets the default HTTP timeout in seconds.</summary>
    public int HttpTimeoutSeconds { get; set; } = 120;

    /**************************************************************/
    /// <summary>Gets or sets the number of transient stateless retries.</summary>
    public int TransientRetryCount { get; set; } = 2;

    /**************************************************************/
    /// <summary>Gets or sets the maximum character count placed in a workbook content preview.</summary>
    public int ContentPreviewCharacterLimit { get; set; } = 30_000;

    /**************************************************************/
    /// <summary>Gets or sets the number of prepared document rows rendered on each console page.</summary>
    public int PreparedResultsPageSize { get; set; } = 5;

    /**************************************************************/
    /// <summary>Gets or sets the maximum extracted characters shown in one console table preview.</summary>
    public int ConsolePreviewCharacterLimit { get; set; } = 120;

    /**************************************************************/
    /// <summary>Gets or sets the default clustering algorithm when no template is selected.</summary>
    public string DefaultAlgorithm { get; set; } = "Lingo";

    /**************************************************************/
    /// <summary>Gets or sets the default clustering language when no template is selected.</summary>
    public string DefaultLanguage { get; set; } = "English";

    #endregion
}
