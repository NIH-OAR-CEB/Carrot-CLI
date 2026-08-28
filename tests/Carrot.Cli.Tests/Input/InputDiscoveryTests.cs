using System.IO.Compression;
using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Carrot.Cli.Input;
using Microsoft.Extensions.Options;
using Xunit;

namespace Carrot.Cli.Tests.Input;

/**************************************************************/
/// <summary>
/// Verifies quoted path normalization and deterministic direct-file, folder, and guarded ZIP discovery.
/// </summary>
public sealed class InputDiscoveryTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Verifies normalized relative-path order, recursion, unsupported-file filtering, and temp-file exclusion.</summary>
    [Fact]
    public async Task FolderDiscoveryIsDeterministicAndSafe()
    {
        #region implementation

        // Arrange
        var root = createTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Nested"));
            await File.WriteAllTextAsync(Path.Combine(root, "b.txt"), "bravo", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(root, "A.md"), "alpha", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(root, "ignore.csv"), "ignored", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(root, "~$owner.docx"), "ignored", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(root, "Nested", "c.txt"), "charlie", TestContext.Current.CancellationToken);
            var loader = createFolderLoader();

            // Act
            var topLevel = await loader.LoadAsync(root, recursive: false, TestContext.Current.CancellationToken);
            var recursive = await loader.LoadAsync(root, recursive: true, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(OperationStatus.Success, topLevel.Status);
            Assert.Equal(["A.md", "b.txt"], topLevel.Value!.Files.Select(file => file.RelativePath));
            Assert.Equal(
                ["A.md", "b.txt", "Nested/c.txt"],
                recursive.Value!.Files.Select(file => file.RelativePath));
            Assert.All(recursive.Value.Files, file => Assert.True(Path.IsPathFullyQualified(file.PhysicalPath)));
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies safe ZIP entries are sorted, extracted, and deleted with their owning batch.</summary>
    [Fact]
    public async Task ZipDiscovery_ValidArchive_SortsAndCleansExtractedFiles()
    {
        #region implementation

        // Arrange
        var root = createTemporaryDirectory();
        try
        {
            var archivePath = Path.Combine(root, "documents.zip");
            createArchive(archivePath, ("z/b.txt", "bravo"), ("A.md", "alpha"), ("ignore.csv", "ignored"));
            var loader = createZipLoader();

            // Act
            var result = await loader.LoadAsync(archivePath, recursive: false, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(OperationStatus.Success, result.Status);
            Assert.Equal(["A.md", "z/b.txt"], result.Value!.Files.Select(file => file.RelativePath));
            var temporaryDirectory = result.Value.TemporaryDirectoryPath!;
            Assert.True(Directory.Exists(temporaryDirectory));
            Assert.All(result.Value.Files, file => Assert.True(File.Exists(file.PhysicalPath)));

            await result.Value.DisposeAsync();
            Assert.False(Directory.Exists(temporaryDirectory));
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies traversal and nested archives are rejected as structured input failures.</summary>
    [Theory]
    [InlineData("../escape.txt", "input.zip.unsafe-path")]
    [InlineData("nested/archive.zip", "input.zip.nested")]
    public async Task ZipDiscovery_UnsafeEntry_ReturnsFailure(string entryName, string expectedCode)
    {
        #region implementation

        // Arrange
        var root = createTemporaryDirectory();
        try
        {
            var archivePath = Path.Combine(root, "unsafe.zip");
            createArchive(archivePath, (entryName, "unsafe"));
            var loader = createZipLoader();

            // Act
            var result = await loader.LoadAsync(archivePath, recursive: false, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(OperationStatus.Failure, result.Status);
            Assert.Null(result.Value);
            Assert.Contains(result.Messages, message => message.Code == expectedCode);
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies matching surrounding quotes are removed before paths become absolute.</summary>
    [Theory]
    [InlineData("document.txt")]
    [InlineData("\"document with spaces.txt\"")]
    [InlineData("'document with spaces.txt'")]
    public void PathNormalizer_QuotedOrUnquotedInput_ReturnsAbsolutePath(string input)
    {
        #region implementation

        // Arrange
        var normalizer = new InputPathNormalizer();

        // Act
        var result = normalizer.Normalize(input);

        // Assert
        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.True(Path.IsPathFullyQualified(result.Value!));
        Assert.DoesNotContain('"', result.Value!);
        Assert.False(result.Value!.StartsWith("'", StringComparison.Ordinal));

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies mismatched surrounding quotes are rejected without throwing.</summary>
    [Theory]
    [InlineData("\"C:\\Documents")]
    [InlineData("'C:\\Documents\"")]
    public void PathNormalizer_MismatchedQuotes_ReturnsStructuredFailure(string input)
    {
        #region implementation

        var result = new InputPathNormalizer().Normalize(input);

        Assert.Equal(OperationStatus.Failure, result.Status);
        Assert.Contains(result.Messages, message => message.Code == "input.path.quotes");

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a folder loader using production defaults.</summary>
    private static FolderInputSourceLoader createFolderLoader()
    {
        #region implementation

        return new FolderInputSourceLoader(new DocumentFormatCatalog(), Options.Create(new CarrotCliOptions()));

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a ZIP loader using production defaults.</summary>
    private static ZipInputSourceLoader createZipLoader()
    {
        #region implementation

        return new ZipInputSourceLoader(new DocumentFormatCatalog(), Options.Create(new CarrotCliOptions()));

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a uniquely named temporary test directory.</summary>
    private static string createTemporaryDirectory()
    {
        #region implementation

        var path = Path.Combine(Path.GetTempPath(), $"carrot-cli-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a ZIP archive from deterministic entry-name and content pairs.</summary>
    private static void createArchive(string archivePath, params (string Name, string Content)[] entries)
    {
        #region implementation

        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        foreach (var entryValue in entries)
        {
            var entry = archive.CreateEntry(entryValue.Name, CompressionLevel.NoCompression);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(entryValue.Content);
        }

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
