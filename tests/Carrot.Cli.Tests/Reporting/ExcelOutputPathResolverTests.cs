using Carrot.Cli.Common;
using Carrot.Cli.Reporting;
using Xunit;

namespace Carrot.Cli.Tests.Reporting;

/**************************************************************/
/// <summary>Verifies quoted, relative, extension, directory, and parent-directory output rules.</summary>
public sealed class ExcelOutputPathResolverTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Verifies quoted absolute paths with spaces and mixed-case extensions normalize successfully.</summary>
    [Fact]
    public void Resolve_QuotedMixedCasePath_ReturnsAbsoluteDestination()
    {
        #region implementation

        // Arrange
        var root = createTemporaryDirectory();
        try
        {
            var expected = Path.Combine(root, "processed results.XLSX");
            var resolver = new ExcelOutputPathResolver();

            // Act
            var result = resolver.Resolve($"  \"{expected}\"  ");

            // Assert
            Assert.Equal(OperationStatus.Success, result.Status);
            Assert.Equal(expected, result.Value);
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies relative workbook paths resolve against the current working directory.</summary>
    [Fact]
    public void Resolve_RelativePath_NormalizesAgainstCurrentDirectory()
    {
        #region implementation

        // Arrange
        var fileName = $"carrot-relative-{Guid.NewGuid():N}.xlsx";
        var resolver = new ExcelOutputPathResolver();

        // Act
        var result = resolver.Resolve(fileName);

        // Assert
        Assert.Equal(Path.GetFullPath(fileName), result.Value);

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies each invalid output class returns its stable structured message code.</summary>
    /// <param name="inputFactory">The invalid path fixture selector.</param>
    /// <param name="expectedCode">The expected stable validation code.</param>
    [Theory]
    [InlineData("empty", "report.path.empty")]
    [InlineData("quotes", "report.path.quotes")]
    [InlineData("extension", "report.path.extension")]
    [InlineData("invalid", "report.path.invalid")]
    [InlineData("missing-parent", "report.path.parent")]
    public void Resolve_InvalidPath_ReturnsExpectedFailure(string inputFactory, string expectedCode)
    {
        #region implementation

        // Arrange
        var root = createTemporaryDirectory();
        try
        {
            var input = inputFactory switch
            {
                "empty" => "   ",
                "quotes" => $"\"{Path.Combine(root, "result.xlsx")}'",
                "extension" => Path.Combine(root, "result.csv"),
                "invalid" => "bad\0path.xlsx",
                "missing-parent" => Path.Combine(root, "missing", "result.xlsx"),
                _ => throw new InvalidOperationException("Unsupported path test fixture.")
            };
            var resolver = new ExcelOutputPathResolver();

            // Act
            var result = resolver.Resolve(input);

            // Assert
            Assert.Equal(OperationStatus.Failure, result.Status);
            Assert.Equal(expectedCode, Assert.Single(result.Messages).Code);
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies an existing directory whose name ends in `.xlsx` is rejected as a file target.</summary>
    [Fact]
    public void Resolve_ExistingDirectoryWithExcelExtension_ReturnsDirectoryFailure()
    {
        #region implementation

        // Arrange
        var root = createTemporaryDirectory();
        try
        {
            var directory = Path.Combine(root, "folder.xlsx");
            Directory.CreateDirectory(directory);
            var resolver = new ExcelOutputPathResolver();

            // Act
            var result = resolver.Resolve(directory);

            // Assert
            Assert.Equal("report.path.directory", Assert.Single(result.Messages).Code);
        }
        finally
        {
            deleteTemporaryDirectory(root);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one isolated filesystem directory for path tests.</summary>
    /// <returns>The absolute owned directory path.</returns>
    private static string createTemporaryDirectory()
    {
        #region implementation

        var path = Path.Combine(Path.GetTempPath(), $"carrot-output-path-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;

        #endregion
    }

    /**************************************************************/
    /// <summary>Deletes one exact owned path-test directory.</summary>
    /// <param name="path">The owned directory.</param>
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
