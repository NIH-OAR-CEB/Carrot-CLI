using System.Text.Json;
using Carrot.Cli.CarrotApi;
using Carrot.Cli.Configuration;
using Carrot.Cli.Extraction;
using Carrot.Cli.Input;
using Microsoft.Extensions.Options;
using Xunit;

namespace Carrot.Cli.Tests.CarrotApi;

/**************************************************************/
/// <summary>Verifies prepared documents map to the complete Carrot request without local metadata.</summary>
public sealed class ClusterRequestFactoryTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Verifies defaults, prepared order, and full document fields form the exact JSON package.</summary>
    [Fact]
    public void Create_PreparedDocuments_PreservesWireContentAndExcludesSourceMetadata()
    {
        #region implementation

        // Arrange
        var factory = new ClusterRequestFactory(Options.Create(new CarrotCliOptions
        {
            DefaultAlgorithm = "STC",
            DefaultLanguage = "French"
        }));
        ExtractedDocument[] documents =
        [
            createDocument(0, "First title", "Complete first content"),
            createDocument(1, "Second title", "Complete second content")
        ];

        // Act
        var request = factory.Create(documents);
        JsonElement json = JsonSerializer.SerializeToElement(request);

        // Assert
        Assert.Equal("STC", request.Algorithm);
        Assert.Equal("French", request.Language);
        Assert.Equal(["First title", "Second title"], request.Documents.Select(document => document.Title));
        Assert.Equal("Complete second content", request.Documents[1].Content);
        Assert.Equal(2, json.GetProperty("documents").GetArrayLength());
        Assert.False(json.GetProperty("documents")[0].TryGetProperty("sourceFile", out _));
        Assert.False(json.GetProperty("documents")[0].TryGetProperty("sha256", out _));
        Assert.False(json.TryGetProperty("parameters", out _));

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies explicit template configuration omits direct fields and preserves nested parameters.</summary>
    [Fact]
    public void Create_ResolvedTemplateConfiguration_MapsExactWireContract()
    {
        #region implementation

        // Arrange
        var factory = new ClusterRequestFactory(Options.Create(new CarrotCliOptions()));
        var nestedValue = JsonSerializer.SerializeToElement(new
        {
            labels = new[] { "one", "two" },
            enabled = true,
            threshold = 1.25D
        });
        var configuration = new ClusteringConfiguration
        {
            Template = "news",
            Parameters = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["nested"] = nestedValue,
                ["formula"] = JsonSerializer.SerializeToElement("=SUM(A1:A2)")
            }
        };

        // Act
        var request = factory.Create(
            [createDocument(0, "Template title", "Template content")],
            configuration);
        var json = JsonSerializer.SerializeToElement(request);

        // Assert
        Assert.Null(request.Algorithm);
        Assert.Null(request.Language);
        Assert.Same(configuration.Parameters, request.Parameters);
        Assert.False(json.TryGetProperty("algorithm", out _));
        Assert.False(json.TryGetProperty("language", out _));
        Assert.Equal(JsonValueKind.Array, json.GetProperty("parameters").GetProperty("nested").GetProperty("labels").ValueKind);
        Assert.True(json.GetProperty("parameters").GetProperty("nested").GetProperty("enabled").GetBoolean());
        Assert.Equal("=SUM(A1:A2)", json.GetProperty("parameters").GetProperty("formula").GetString());

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies both request-factory entry points guard required arguments.</summary>
    [Fact]
    public void Create_NullArguments_ThrowArgumentNullException()
    {
        #region implementation

        // Arrange
        var factory = new ClusterRequestFactory(Options.Create(new CarrotCliOptions()));
        ExtractedDocument[] documents = [createDocument(0, "Title", "Content")];

        // Act and assert
        Assert.Throws<ArgumentNullException>(() => factory.Create(null!));
        Assert.Throws<ArgumentNullException>(() => factory.Create(null!, new ClusteringConfiguration()));
        Assert.Throws<ArgumentNullException>(() => factory.Create(documents, null!));

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one extracted-document fixture with source metadata that must stay client-side.</summary>
    /// <param name="index">The contiguous Carrot document index.</param>
    /// <param name="title">The title expected on the wire.</param>
    /// <param name="content">The complete content expected on the wire.</param>
    /// <returns>The extracted document fixture.</returns>
    private static ExtractedDocument createDocument(int index, string title, string content)
    {
        #region implementation

        var path = $"C:\\Inputs\\document-{index + 1}.txt";
        return new ExtractedDocument
        {
            SourceFile = new SourceFile
            {
                SourceOrdinal = index,
                SourceKey = path,
                ContainerPath = "C:\\Inputs",
                PhysicalPath = path,
                RelativePath = Path.GetFileName(path),
                FileName = Path.GetFileName(path),
                Extension = ".txt",
                SizeBytes = content.Length
            },
            CarrotDocumentIndex = index,
            Title = title,
            Content = content,
            Sha256 = new string('A', 64)
        };

        #endregion
    }

    #endregion
}
