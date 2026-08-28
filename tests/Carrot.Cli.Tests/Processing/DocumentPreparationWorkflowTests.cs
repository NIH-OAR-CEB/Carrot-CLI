using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Carrot.Cli.Extraction;
using Carrot.Cli.Extraction.Extractors;
using Carrot.Cli.Input;
using Carrot.Cli.Processing;
using Microsoft.Extensions.Options;
using Xunit;

namespace Carrot.Cli.Tests.Processing;

/**************************************************************/
/// <summary>Verifies multi-input preparation ordering, deduplication, previews, and review-only failures.</summary>
public sealed class DocumentPreparationWorkflowTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Verifies mixed direct and folder inputs retain first occurrences and prepare successful rows in order.</summary>
    [Fact]
    public async Task PrepareAsync_MixedDuplicateInputs_ReturnsOrderedPartialBatch()
    {
        #region implementation

        // Arrange
        var root = createTemporaryDirectory();
        try
        {
            var folder = Path.Combine(root, "Documents");
            Directory.CreateDirectory(folder);
            var firstPath = Path.Combine(folder, "a.txt");
            await File.WriteAllTextAsync(
                firstPath,
                "Alpha\r\ncontent [with] markup and enough characters to truncate.",
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(folder, "b.txt"),
                "Bravo content",
                TestContext.Current.CancellationToken);
            var workflow = createWorkflow(new CarrotCliOptions { ConsolePreviewCharacterLimit = 20 });

            // Act
            var result = await workflow.PrepareAsync(
                new PrepareDocumentsRequest { InputPaths = [firstPath, folder] },
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(OperationStatus.PartialSuccess, result.Status);
            Assert.Equal(2, result.Value!.Documents.Count);
            Assert.Equal(2, result.Value.Rows.Count);
            Assert.Equal([0, 1], result.Value.Documents.Select(document => document.CarrotDocumentIndex));
            Assert.Equal(["a.txt", "b.txt"], result.Value.Rows.Select(row => row.RelativePath));
            Assert.Equal("Alpha content [with]", result.Value.Rows[0].ContentPreview);
            Assert.True(result.Value.Rows[0].PreviewTruncated);
            Assert.Contains(result.Messages, message => message.Code == "preparation.duplicate");
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies a completely failed extraction retains its row for review while exposing no processable documents.</summary>
    [Fact]
    public async Task PrepareAsync_AllFilesFail_RetainsReviewRowsInFailureResult()
    {
        #region implementation

        // Arrange
        var root = createTemporaryDirectory();
        try
        {
            var path = Path.Combine(root, "empty.txt");
            await File.WriteAllTextAsync(path, " \r\n ", TestContext.Current.CancellationToken);
            var workflow = createWorkflow(new CarrotCliOptions());

            // Act
            var result = await workflow.PrepareAsync(
                new PrepareDocumentsRequest { InputPaths = [path] },
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(OperationStatus.Failure, result.Status);
            Assert.NotNull(result.Value);
            Assert.Empty(result.Value.Documents);
            var row = Assert.Single(result.Value.Rows);
            Assert.Equal(PreparedDocumentStatus.Failed, row.Status);
            Assert.Contains("no searchable text", row.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates the preparation workflow with production input services and the plain-text extractor.</summary>
    private static DocumentPreparationWorkflow createWorkflow(CarrotCliOptions configuredOptions)
    {
        #region implementation

        var options = Options.Create(configuredOptions);
        var formats = new DocumentFormatCatalog();
        var folderLoader = new FolderInputSourceLoader(formats, options);
        var fileLoader = new FileInputSourceLoader(formats, options);
        var zipLoader = new ZipInputSourceLoader(formats, options);
        var resolver = new InputSourceResolver(folderLoader, fileLoader, zipLoader, formats);
        var resultFactory = new ExtractionResultFactory(new HashService());
        var coordinator = new DocumentExtractionCoordinator(
            [new PlainTextExtractor(resultFactory)],
            options);
        return new DocumentPreparationWorkflow(
            new InputPathNormalizer(),
            resolver,
            coordinator,
            options);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a uniquely named temporary test directory.</summary>
    private static string createTemporaryDirectory()
    {
        #region implementation

        var path = Path.Combine(Path.GetTempPath(), $"carrot-cli-preparation-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;

        #endregion
    }

    /**************************************************************/
    /// <summary>Deletes an owned test directory after resolving its exact absolute path.</summary>
    private static void deleteTemporaryDirectory(string path)
    {
        #region implementation

        var fullPath = Path.GetFullPath(path);
        if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, recursive: true);
        }

        #endregion
    }

    #endregion
}
