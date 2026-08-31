using Carrot.Cli.CarrotApi.Contracts;
using Carrot.Cli.Extraction;
using Carrot.Cli.Input;
using Carrot.Cli.Processing;
using Carrot.Cli.Reporting;

namespace Carrot.Cli.Tests.Reporting;

/**************************************************************/
/// <summary>
/// Creates deterministic retained processed batches shared by reporting and interactive export tests.
/// </summary>
internal static class ReportingTestData
{
    #region implementation

    internal static readonly Guid RunId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    /**************************************************************/
    /// <summary>Creates one retained processed batch with one submitted document.</summary>
    /// <param name="content">The complete retained extracted content.</param>
    /// <param name="memberships">The optional ordered memberships; an omitted value creates an unassigned row.</param>
    /// <param name="fileName">The source filename used by the row.</param>
    /// <returns>A complete deterministic processed batch.</returns>
    internal static ProcessedDocumentBatch CreateProcessedBatch(
        string content = "Complete retained content",
        IReadOnlyList<ClusterMembership>? memberships = null,
        string fileName = "document.txt")
    {
        #region implementation

        var sourcePath = Path.Combine("C:\\Inputs", fileName);
        var document = new ExtractedDocument
        {
            SourceFile = new SourceFile
            {
                SourceOrdinal = 7,
                SourceKey = sourcePath,
                ContainerPath = "C:\\Inputs",
                PhysicalPath = sourcePath,
                RelativePath = fileName,
                FileName = fileName,
                Extension = ".txt",
                SizeBytes = 1234
            },
            CarrotDocumentIndex = 0,
            Title = Path.GetFileNameWithoutExtension(fileName),
            Content = content,
            Sha256 = new string('A', 64)
        };

        return new ProcessedDocumentBatch
        {
            RunId = RunId,
            Endpoint = new Uri("http://localhost:8080/service"),
            Request = new ClusterRequest
            {
                Algorithm = "Lingo",
                Language = "English",
                Documents = [new ClusterDocument { Title = document.Title, Content = document.Content }]
            },
            Response = new ClusterResponse(),
            Rows =
            [
                new ProcessedDocumentRow
                {
                    CarrotDocumentIndex = 0,
                    PreparedDocument = document,
                    Memberships = memberships ?? Array.Empty<ClusterMembership>()
                }
            ]
        };

        #endregion
    }

    #endregion
}
