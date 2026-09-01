using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Carrot.Cli.Configuration;
using Carrot.Cli.Processing;
using Microsoft.Extensions.Options;

namespace Carrot.Cli.Reporting;

/**************************************************************/
/// <summary>
/// Maps one retained successful processed batch to the ordered membership-row workbook contract.
/// </summary>
/// <remarks>
/// Mapping consumes only retained in-memory data. It never rereads source files or contacts
/// Carrot, so exporting cannot change the processed result being represented.
/// </remarks>
/// <seealso cref="ProcessedDocumentBatch"/>
/// <seealso cref="ReportRequest"/>
internal sealed class ProcessedDocumentReportMapper
{
    #region implementation

    private static readonly JsonSerializerOptions MembershipJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly int _contentPreviewCharacterLimit;

    /**************************************************************/
    /// <summary>Initializes report mapping with the validated workbook preview limit.</summary>
    /// <param name="options">The validated CLI safeguards and defaults.</param>
    public ProcessedDocumentReportMapper(IOptions<CarrotCliOptions> options)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(options);
        _contentPreviewCharacterLimit = options.Value.ContentPreviewCharacterLimit;

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one ordered report request with one row per category membership.</summary>
    /// <param name="batch">The exact retained request, response, and correlated rows.</param>
    /// <param name="outputPath">The normalized absolute workbook destination.</param>
    /// <param name="overwrite">Whether an existing workbook may be replaced.</param>
    /// <returns>
    /// A complete workbook request with one row per category membership and one blank-category
    /// row for every unassigned submitted document.
    /// </returns>
    internal ReportRequest Create(ProcessedDocumentBatch batch, string outputPath, bool overwrite)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(batch);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var rows = batch.Rows
            .OrderBy(row => row.CarrotDocumentIndex)
            .SelectMany(row => createRows(batch, row))
            .ToArray();

        return new ReportRequest
        {
            OutputPath = outputPath,
            Overwrite = overwrite,
            Rows = Array.AsReadOnly(rows)
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>Expands one correlated processed document into ordered membership rows.</summary>
    /// <param name="batch">The owning processed run.</param>
    /// <param name="row">The submitted document and its ordered memberships.</param>
    /// <returns>
    /// One row per membership, or one row with blank category values when the document is unassigned.
    /// </returns>
    private IReadOnlyList<ReportRow> createRows(ProcessedDocumentBatch batch, ProcessedDocumentRow row)
    {
        #region implementation

        if (row.Memberships.Count == 0)
        {
            return [createRow(batch, row, membership: null)];
        }

        return row.Memberships
            .Select(membership => createRow(batch, row, membership))
            .ToArray();

        #endregion
    }

    /**************************************************************/
    /// <summary>Maps one document and optional category membership to one database-friendly worksheet row.</summary>
    /// <param name="batch">The owning processed run.</param>
    /// <param name="row">The submitted document whose metadata is repeated on the row.</param>
    /// <param name="membership">The single membership represented by the row, or null for an unassigned document.</param>
    /// <returns>The complete scalar-category workbook row.</returns>
    private ReportRow createRow(
        ProcessedDocumentBatch batch,
        ProcessedDocumentRow row,
        ClusterMembership? membership)
    {
        #region implementation

        var document = row.PreparedDocument;
        var source = document.SourceFile;
        var previewLength = Math.Min(document.Content.Length, _contentPreviewCharacterLimit);

        return new ReportRow
        {
            RunId = batch.RunId,
            RunStatus = "Success",
            Endpoint = batch.Endpoint.AbsoluteUri,
            Algorithm = batch.Request.Algorithm,
            Language = batch.Request.Language,
            Template = batch.Template,
            SourceOrdinal = source.SourceOrdinal,
            CarrotDocumentIndex = row.CarrotDocumentIndex,
            ContainerPath = source.ContainerPath,
            RelativePath = source.RelativePath,
            FileName = source.FileName,
            Extension = source.Extension,
            SizeBytes = source.SizeBytes,
            Sha256 = document.Sha256,
            ExtractionStatus = "Ready",
            ErrorMessage = null,
            ExtractedCharacterCount = document.Content.Length,
            ContentPreview = document.Content[..previewLength],
            PreviewTruncated = document.Content.Length > previewLength,
            CategoryCount = membership is null ? 0 : 1,
            CategoryPaths = membership?.CategoryPath,
            CategoryScores = membership?.Score?.ToString("R", CultureInfo.InvariantCulture),
            CategoryMembershipsJson = membership is null
                ? "[]"
                : JsonSerializer.Serialize(new[] { membership }, MembershipJsonOptions)
        };

        #endregion
    }

    #endregion
}
