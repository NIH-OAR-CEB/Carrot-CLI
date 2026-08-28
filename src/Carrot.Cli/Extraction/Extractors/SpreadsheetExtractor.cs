using Carrot.Cli.Input;
using ClosedXML.Excel;
using System.Text;

namespace Carrot.Cli.Extraction.Extractors;

/**************************************************************/
/// <summary>
/// Defines sheet-ordered nonempty-cell text extraction from XLSX workbooks.
/// </summary>
/// <seealso cref="IDocumentTextExtractor"/>
internal sealed class SpreadsheetExtractor : IDocumentTextExtractor
{
    #region implementation

    private static readonly IReadOnlySet<string> Extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".xlsx" };
    private readonly ExtractionResultFactory _resultFactory;

    /**************************************************************/
    /// <summary>Initializes spreadsheet extraction with common result construction.</summary>
    public SpreadsheetExtractor(ExtractionResultFactory resultFactory)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(resultFactory);
        _resultFactory = resultFactory;

        #endregion
    }

    /**************************************************************/
    /// <summary>Gets the XLSX extension handled by this extractor.</summary>
    public IReadOnlySet<string> SupportedExtensions
    {
        get
        {
            #region implementation

            return Extensions;

            #endregion
        }
    }

    /**************************************************************/
    /// <summary>Extracts sheet-ordered nonempty cell text from one XLSX source.</summary>
    public async Task<ExtractionResult> ExtractAsync(SourceFile sourceFile, CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(sourceFile);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var workbook = new XLWorkbook(sourceFile.PhysicalPath);
            var content = new StringBuilder();
            foreach (var worksheet in workbook.Worksheets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var usedRange = worksheet.RangeUsed();
                if (usedRange is null)
                {
                    continue;
                }

                content.AppendLine(worksheet.Name);
                foreach (var row in usedRange.RowsUsed())
                {
                    var values = row.CellsUsed()
                        .Select(cell => cell.GetFormattedString())
                        .Where(value => !string.IsNullOrWhiteSpace(value));
                    var line = string.Join('\t', values);
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        content.AppendLine(line);
                    }
                }
            }

            return await _resultFactory
                .CreateSuccessAsync(sourceFile, content.ToString(), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return _resultFactory.CreateFailure(
                sourceFile,
                "extraction.xlsx",
                $"Unable to read spreadsheet content: {exception.Message}");
        }

        #endregion
    }

    #endregion
}
