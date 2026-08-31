using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Carrot.Cli.Configuration;
using Carrot.Cli.Processing;
using Microsoft.Extensions.Options;

namespace Carrot.Cli.Reporting;

/**************************************************************/
/// <summary>
/// Maps one retained successful processed batch to the shared ordered workbook row contract.
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
    /// <summary>Creates one ordered report request from a retained successful processed batch.</summary>
    /// <param name="batch">The exact retained request, response, and correlated rows.</param>
    /// <param name="outputPath">The normalized absolute workbook destination.</param>
    /// <param name="overwrite">Whether an existing workbook may be replaced.</param>
    /// <returns>A complete workbook request with one row per submitted document.</returns>
    internal ReportRequest Create(ProcessedDocumentBatch batch, string outputPath, bool overwrite)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(batch);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var rows = batch.Rows
            .OrderBy(row => row.CarrotDocumentIndex)
            .Select(row => createRow(batch, row))
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
    /// <summary>Maps one correlated processed document to the documented Results worksheet schema.</summary>
    /// <param name="batch">The owning processed run.</param>
    /// <param name="row">The submitted document and its ordered memberships.</param>
    /// <returns>The complete workbook row.</returns>
    private ReportRow createRow(ProcessedDocumentBatch batch, ProcessedDocumentRow row)
    {
        #region implementation

        var document = row.PreparedDocument;
        var source = document.SourceFile;
        var previewLength = Math.Min(document.Content.Length, _contentPreviewCharacterLimit);
        var categoryPaths = row.Memberships.Count == 0
            ? null
            : string.Join(Environment.NewLine, row.Memberships.Select(membership => membership.CategoryPath));
        var categoryScores = row.Memberships.Count == 0
            ? null
            : string.Join(
                Environment.NewLine,
                row.Memberships.Select(membership => membership.Score?.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty));

        return new ReportRow
        {
            RunId = batch.RunId,
            RunStatus = "Success",
            Endpoint = batch.Endpoint.AbsoluteUri,
            Algorithm = batch.Request.Algorithm,
            Language = batch.Request.Language,
            Template = null,
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
            CategoryCount = row.Memberships.Count,
            CategoryPaths = categoryPaths,
            CategoryScores = categoryScores,
            CategoryMembershipsJson = JsonSerializer.Serialize(row.Memberships, MembershipJsonOptions)
        };

        #endregion
    }

    #endregion
}
