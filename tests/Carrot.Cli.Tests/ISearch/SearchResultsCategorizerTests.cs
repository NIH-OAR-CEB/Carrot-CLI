using System.Text.Json;
using Carrot.Cli.CarrotApi;
using Carrot.Cli.CarrotApi.Contracts;
using Carrot.Cli.Common;
using Carrot.Cli.ISearch;
using Carrot.Cli.ISearch.Contracts;
using Carrot.Cli.Reporting;
using Xunit;

namespace Carrot.Cli.Tests.ISearch;

/**************************************************************/
/// <summary>Verifies exact iSearch-to-Carrot mapping and indexed category projection.</summary>
public sealed class SearchResultsCategorizerTests
{
    #region implementation

    private static readonly Uri Endpoint = new("http://localhost:8080/service");

    /**************************************************************/
    /// <summary>Ensures only the four requested fields are submitted and memberships map by index.</summary>
    [Fact]
    public async Task CategorizeAsync_SubmitsExactFieldsAndCorrelatesMemberships()
    {
        #region implementation

        // Arrange
        var carrot = new CapturingCategorizer
        {
            Result = createCarrotResult(
                [
                    [new ClusterMembership { CarrotDocumentIndex = 0, CategoryPath = "Health", Score = 1.5D }],
                    Array.Empty<ClusterMembership>()
                ])
        };
        var session = createSession(
            createRecord("1", "First", "Abstract one", "Aims one"),
            createRecord("2", "Second", "Abstract two", "Aims two"));
        var categorizer = new SearchResultsCategorizer(carrot);

        // Act
        var result = await categorizer.CategorizeAsync(
            session,
            Endpoint,
            TimeSpan.FromSeconds(120),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.NotNull(carrot.Request);
        Assert.Equal(2, carrot.Request!.Documents.Count);
        var firstJson = JsonSerializer.SerializeToElement(carrot.Request.Documents[0]);
        Assert.Equal(4, firstJson.EnumerateObject().Count());
        Assert.Equal("1", firstJson.GetProperty("nihApplId").GetString());
        Assert.Equal("First", firstJson.GetProperty("title").GetString());
        Assert.Equal("Abstract one", firstJson.GetProperty("abstract").GetString());
        Assert.Equal("Aims one", firstJson.GetProperty("specificAims").GetString());
        Assert.False(firstJson.TryGetProperty("content", out _));
        Assert.Single(result.Value!.Rows[0].Memberships);
        Assert.Empty(result.Value.Rows[1].Memberships);
        Assert.Equal(1, result.Value.UnassignedCount);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures iSearch numeric and null scalars become valid Carrot text without changing retained values.</summary>
    [Fact]
    public async Task CategorizeAsync_NormalizesCarrotScalarValues()
    {
        #region implementation

        // Arrange
        var carrot = new CapturingCategorizer
        {
            Result = createCarrotResult([Array.Empty<ClusterMembership>()])
        };
        var record = JsonSerializer.SerializeToElement(new
        {
            nihApplId = 12345,
            title = (string?)null,
            @abstract = "Abstract",
            specificAims = (string?)null
        });
        var session = createSession(record);

        // Act
        var result = await new SearchResultsCategorizer(carrot).CategorizeAsync(
            session,
            Endpoint,
            TimeSpan.FromSeconds(120),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OperationStatus.Success, result.Status);
        var serializedDocument = JsonSerializer.SerializeToElement(carrot.Request!.Documents[0]);
        Assert.Equal("12345", serializedDocument.GetProperty("nihApplId").GetString());
        Assert.Equal(string.Empty, serializedDocument.GetProperty("title").GetString());
        Assert.Equal("Abstract", serializedDocument.GetProperty("abstract").GetString());
        Assert.Equal(string.Empty, serializedDocument.GetProperty("specificAims").GetString());
        Assert.Equal(JsonValueKind.Number, result.Value!.Rows[0].NihApplId.ValueKind);
        Assert.Equal(JsonValueKind.Null, result.Value.Rows[0].Title.ValueKind);
        Assert.Equal(JsonValueKind.Null, result.Value.Rows[0].SpecificAims.ValueKind);

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures a missing selected field is tolerated and sent as empty Carrot text.</summary>
    [Fact]
    public async Task CategorizeAsync_MissingSelectedField_ToleratesMissingValue()
    {
        #region implementation

        // Arrange
        var carrot = new CapturingCategorizer
        {
            Result = createCarrotResult([Array.Empty<ClusterMembership>()])
        };
        var session = createSession(JsonSerializer.SerializeToElement(new
        {
            nihApplId = "1",
            title = "First",
            @abstract = "Abstract"
        }));

        // Act
        var result = await new SearchResultsCategorizer(carrot).CategorizeAsync(
            session,
            Endpoint,
            TimeSpan.FromSeconds(120),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OperationStatus.Success, result.Status);
        var serializedDocument = JsonSerializer.SerializeToElement(carrot.Request!.Documents[0]);
        Assert.Equal(string.Empty, serializedDocument.GetProperty("specificAims").GetString());
        Assert.Equal(JsonValueKind.Null, result.Value!.Rows[0].SpecificAims.ValueKind);

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates a session containing the supplied records in one valid service page.</summary>
    /// <param name="records">The generic iSearch records.</param>
    /// <returns>A session retaining the records.</returns>
    private static SearchResultPageSession createSession(params JsonElement[] records) =>
        new(
            new StubSearchClient(),
            new SearchRequest
            {
                Dataset = "grants",
                Query = "example",
                Fields = ["nihApplId", "title", "abstract", "specificAims"],
                Rows = 100
            },
            new SearchResponse
            {
                Cardinality = new SearchCardinality
                {
                    TotalResults = records.Length,
                    CurrentResults = records.Length,
                    PageNumber = records.Length == 0 ? 0 : 1,
                    TotalPages = records.Length == 0 ? 0 : 1
                },
                Results = records
            });

    /**************************************************************/
    /// <summary>Creates one record with the exact four categorization fields.</summary>
    /// <param name="nihApplId">The application identifier.</param>
    /// <param name="title">The title.</param>
    /// <param name="abstractText">The abstract.</param>
    /// <param name="specificAims">The specific aims.</param>
    /// <returns>A detached JSON record.</returns>
    private static JsonElement createRecord(
        string nihApplId,
        string title,
        string abstractText,
        string specificAims) => JsonSerializer.SerializeToElement(new
        {
            nihApplId,
            title,
            @abstract = abstractText,
            specificAims,
            unrelated = "not submitted"
        });

    /**************************************************************/
    /// <summary>Creates a successful shared Carrot result with indexed memberships.</summary>
    /// <param name="membershipsByDocument">Membership lists aligned to submitted indexes.</param>
    /// <returns>A successful category result.</returns>
    private static CarrotCategorizationResult createCarrotResult(
        IReadOnlyList<IReadOnlyList<ClusterMembership>> membershipsByDocument) => new()
        {
            RunId = Guid.Parse("11111111-2222-3333-4444-555555555555"),
            Endpoint = Endpoint,
            Request = new ClusterRequest { Documents = [] },
            Configuration = new Carrot.Cli.Configuration.ClusteringConfiguration
            {
                Algorithm = "Lingo",
                Language = "English"
            },
            Response = new ClusterResponse(),
            MembershipsByDocument = membershipsByDocument
        };

    /**************************************************************/
    /// <summary>Provides the minimum iSearch continuation implementation required by the session.</summary>
    private sealed class StubSearchClient : IISearchApiClient
    {
        #region implementation

        public Task<OperationResult<SearchHealthResponse>> GetHealthAsync(CancellationToken cancellationToken) =>
            Task.FromResult(OperationResult<SearchHealthResponse>.Success(new SearchHealthResponse()));

        public Task<OperationResult<IReadOnlyList<string>>> GetDatasetsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(OperationResult<IReadOnlyList<string>>.Success(Array.Empty<string>()));

        public Task<OperationResult<IReadOnlyList<SearchField>>> GetFieldsAsync(
            string dataset,
            CancellationToken cancellationToken) =>
            Task.FromResult(OperationResult<IReadOnlyList<SearchField>>.Success(Array.Empty<SearchField>()));

        public Task<OperationResult<SearchResponse>> SearchAsync(
            SearchRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(OperationResult<SearchResponse>.Failure(
            [new OperationMessage
            {
                Code = "test.unexpected",
                Message = "No initial search expected.",
                Severity = OperationMessageSeverity.Error
            }]));

        public Task<OperationResult<SearchResponse>> SearchNextPageAsync(
            SearchRequest request,
            string cursor,
            int pageNumber,
            CancellationToken cancellationToken) =>
            Task.FromResult(OperationResult<SearchResponse>.Failure(
            [new OperationMessage
            {
                Code = "test.unexpected",
                Message = "No continuation expected.",
                Severity = OperationMessageSeverity.Error
            }]));

        #endregion
    }

    /**************************************************************/
    /// <summary>Captures the shared categorization request without contacting Carrot.</summary>
    private sealed class CapturingCategorizer : ICarrotCategorizer
    {
        #region implementation

        public CarrotCategorizationRequest? Request { get; private set; }
        public CarrotCategorizationResult? Result { get; init; }

        public Task<OperationResult<CarrotCategorizationResult>> CategorizeAsync(
            CarrotCategorizationRequest request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(OperationResult<CarrotCategorizationResult>.Success(Result!));
        }

        #endregion
    }

    #endregion
}
