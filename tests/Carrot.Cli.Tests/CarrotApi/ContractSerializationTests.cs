using System.Text.Json;
using Carrot.Cli.CarrotApi;
using Carrot.Cli.CarrotApi.Contracts;
using Xunit;

namespace Carrot.Cli.Tests.CarrotApi;

/**************************************************************/
/// <summary>
/// Verifies the stubbed wire contracts against the supplied Carrot 4.8.6 OpenAPI examples.
/// </summary>
/// <remarks>
/// These tests intentionally perform only in-memory JSON serialization and reflection. They do
/// not invoke the layout-only HTTP client or require a live Carrot service.
/// </remarks>
public sealed class ContractSerializationTests
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Gets the Lingo, STC, and Bisecting K-Means parameter samples supplied for validation.
    /// </summary>
    /// <remarks>
    /// The samples cover nested objects, numeric and string scalars, arrays, spaces in algorithm
    /// names, and the special <c>@type</c> discriminator key.
    /// </remarks>
    public static TheoryData<string, string> ParameterSamples
    {
        get
        {
            #region implementation

            return new TheoryData<string, string>
            {
                {
                    """
                    {
                      "algorithm": "Lingo",
                      "language": "English",
                      "parameters": {
                        "desiredClusterCount": 15,
                        "preprocessing": {
                          "documentAssigner": { "minClusterSize": 5 },
                          "labelFilters": {
                            "minLengthLabelFilter": { "minLength": 4 }
                          }
                        },
                        "clusterBuilder": { "phraseLabelBoost": 2 },
                        "matrixBuilder": {
                          "boostFields": [ "Title" ],
                          "boostedFieldWeight": 3
                        }
                      }
                    }
                    """,
                    "Lingo"
                },
                {
                    """
                    {
                      "algorithm": "STC",
                      "language": "English",
                      "parameters": {
                        "maxClusters": 25,
                        "maxBaseClusters": 100,
                        "minBaseClusterScore": 4,
                        "minBaseClusterSize": 15,
                        "documentCountBoost": "2"
                      }
                    }
                    """,
                    "STC"
                },
                {
                    """
                    {
                      "algorithm": "Bisecting K-Means",
                      "language": "English",
                      "parameters": {
                        "clusterCount": 20,
                        "maxIterations": 50,
                        "partitionCount": 4,
                        "labelCount": 4,
                        "matrixBuilder": {
                          "boostFields": [ "Title" ],
                          "boostedFieldWeight": 3
                        },
                        "matrixReducer": {
                          "factorizationFactory": {
                            "@type": "KMeansMatrixFactorizationFactory",
                            "factorizationQuality": "MEDIUM"
                          }
                        }
                      }
                    }
                    """,
                    "Bisecting K-Means"
                }
            };

            #endregion
        }
    }

    /**************************************************************/
    /// <summary>
    /// Verifies every supplied parameter sample deserializes and round-trips without shape loss.
    /// </summary>
    /// <param name="sampleJson">The complete sample request fragment.</param>
    /// <param name="expectedAlgorithm">The algorithm identifier expected after deserialization.</param>
    /// <remarks>
    /// Structural comparison of the parameters subtree proves preservation of nested objects,
    /// arrays, strings, numbers, and special property names without coupling tests to key order.
    /// </remarks>
    [Theory]
    [MemberData(nameof(ParameterSamples))]
    public void ClusterRequestAcceptsSuppliedParameterSamples(string sampleJson, string expectedAlgorithm)
    {
        #region implementation

        // Arrange
        using JsonDocument sourceDocument = JsonDocument.Parse(sampleJson);

        // Act
        ClusterRequest? request = JsonSerializer.Deserialize<ClusterRequest>(sampleJson);
        JsonElement serializedRequest = JsonSerializer.SerializeToElement(request);

        // Assert
        Assert.NotNull(request);
        Assert.Equal(expectedAlgorithm, request!.Algorithm);
        Assert.Equal("English", request.Language);
        Assert.NotNull(request.Parameters);
        Assert.Empty(request.Documents);
        Assert.True(JsonElement.DeepEquals(
            sourceDocument.RootElement.GetProperty("parameters"),
            serializedRequest.GetProperty("parameters")));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies a cluster request accepts the arbitrary document fields shown in the API examples.
    /// </summary>
    /// <remarks>
    /// The example deliberately includes both scalar string fields and the documented array-valued
    /// field, even though the OpenAPI <c>additionalProperties</c> declaration says string.
    /// </remarks>
    [Fact]
    public void ClusterRequestAcceptsOpenApiDocumentFieldExamples()
    {
        #region implementation

        // Arrange
        const string requestJson = """
            {
              "language": "English",
              "algorithm": "Lingo",
              "documents": [
                { "field1": "doc. 1, some value" },
                {
                  "field1": "doc. 2, some value",
                  "field2": "another value",
                  "field3": [ "multiple-entry field value 1", "value 2" ]
                }
              ]
            }
            """;

        // Act
        ClusterRequest? request = JsonSerializer.Deserialize<ClusterRequest>(requestJson);
        JsonElement serializedRequest = JsonSerializer.SerializeToElement(request);

        // Assert
        Assert.NotNull(request);
        Assert.Equal(2, request!.Documents.Count);
        Assert.Null(request.Documents[0].Title);
        Assert.Null(request.Documents[0].Content);
        Assert.Equal(JsonValueKind.String, request.Documents[1].AdditionalFields["field2"].ValueKind);
        Assert.Equal(JsonValueKind.Array, request.Documents[1].AdditionalFields["field3"].ValueKind);
        Assert.False(serializedRequest.GetProperty("documents")[1].TryGetProperty("additionalFields", out _));
        Assert.Equal(JsonValueKind.Array, serializedRequest.GetProperty("documents")[1].GetProperty("field3").ValueKind);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies the planned title/content fields coexist with arbitrary document extension fields.
    /// </summary>
    [Fact]
    public void ClusterDocumentRetainsPlannedAndArbitraryFieldsAsPeers()
    {
        #region implementation

        // Arrange
        var document = new ClusterDocument
        {
            Title = "Source title",
            Content = "Complete extracted text",
            AdditionalFields = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["tags"] = JsonSerializer.SerializeToElement(new[] { "one", "two" })
            }
        };

        // Act
        JsonElement serializedDocument = JsonSerializer.SerializeToElement(document);

        // Assert
        Assert.Equal("Source title", serializedDocument.GetProperty("title").GetString());
        Assert.Equal("Complete extracted text", serializedDocument.GetProperty("content").GetString());
        Assert.Equal(JsonValueKind.Array, serializedDocument.GetProperty("tags").ValueKind);
        Assert.False(serializedDocument.TryGetProperty("additionalFields", out _));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies recursive cluster response labels, indexes, scores, and children deserialize exactly.
    /// </summary>
    [Fact]
    public void ClusterResponseAcceptsOpenApiResponseExample()
    {
        #region implementation

        // Arrange
        const string responseJson = """
            {
              "clusters": [
                {
                  "labels": [ "ABC" ],
                  "documents": [ 0, 2, 4 ],
                  "clusters": [
                    {
                      "labels": [ "Nested" ],
                      "documents": [ 2 ],
                      "clusters": [],
                      "score": 10.5
                    }
                  ],
                  "score": 120.2
                },
                {
                  "labels": [ "Foo", "Bar" ],
                  "documents": [ 1, 5 ],
                  "clusters": [],
                  "score": 20
                }
              ]
            }
            """;

        // Act
        ClusterResponse? response = JsonSerializer.Deserialize<ClusterResponse>(responseJson);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(2, response!.Clusters.Count);
        Assert.Equal(new[] { 0, 2, 4 }, response.Clusters[0].Documents);
        Assert.Equal(120.2D, response.Clusters[0].Score);
        Assert.Single(response.Clusters[0].Clusters);
        Assert.Equal("Nested", response.Clusters[0].Clusters[0].Labels[0]);
        Assert.Equal(new[] { "Foo", "Bar" }, response.Clusters[1].Labels);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies required list maps, arbitrary template bodies, and error details deserialize exactly.
    /// </summary>
    [Fact]
    public void ListAndErrorResponsesAcceptOpenApiExamples()
    {
        #region implementation

        // Arrange
        const string listJson = """
            {
              "algorithms": {
                "Bisecting K-Means": [ "English", "French" ],
                "Lingo": [ "English", "French" ],
                "STC": [ "English", "French" ]
              },
              "templates": {
                "frontend-default": { "algorithm": "English", "language": "Lingo" },
                "stc": { "algorithm": "STC" }
              }
            }
            """;
        const string errorJson = """
            {
              "type": "BAD_REQUEST",
              "message": "Could not parse request body.",
              "exception": "com.fasterxml.jackson.databind.exc.MismatchedInputException",
              "stacktrace": "..."
            }
            """;

        // Act
        ListResponse? listResponse = JsonSerializer.Deserialize<ListResponse>(listJson);
        CarrotErrorResponse? errorResponse = JsonSerializer.Deserialize<CarrotErrorResponse>(errorJson);

        // Assert
        Assert.NotNull(listResponse);
        Assert.Equal(new[] { "English", "French" }, listResponse!.Algorithms["Lingo"]);
        Assert.Equal("STC", listResponse.Templates["stc"].GetProperty("algorithm").GetString());
        Assert.NotNull(errorResponse);
        Assert.Equal(CarrotErrorType.BadRequest, errorResponse!.Type);
        Assert.Equal("Could not parse request body.", errorResponse.Message);
        Assert.Equal("com.fasterxml.jackson.databind.exc.MismatchedInputException", errorResponse.Exception);
        Assert.Equal("...", errorResponse.StackTrace);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies the list response rejects either OpenAPI-required map when it is absent.
    /// </summary>
    /// <param name="incompleteJson">A list response missing algorithms or templates.</param>
    [Theory]
    [InlineData("""{ "algorithms": {} }""")]
    [InlineData("""{ "templates": {} }""")]
    public void ListResponseRequiresAlgorithmsAndTemplates(string incompleteJson)
    {
        #region implementation

        // Act and assert
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ListResponse>(incompleteJson));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies the error response rejects either OpenAPI-required field when it is absent.
    /// </summary>
    /// <param name="incompleteJson">An error response missing its type or message.</param>
    [Theory]
    [InlineData("""{ "type": "BAD_REQUEST" }""")]
    [InlineData("""{ "message": "Could not parse request body." }""")]
    public void ErrorResponseRequiresTypeAndMessage(string incompleteJson)
    {
        #region implementation

        // Act and assert
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CarrotErrorResponse>(incompleteJson));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies every problem type enumerated by the OpenAPI schema maps to its typed value.
    /// </summary>
    /// <param name="wireValue">The exact uppercase JSON problem-type value.</param>
    /// <param name="expectedTypeName">The expected typed error-value name.</param>
    [Theory]
    [InlineData("BAD_REQUEST", nameof(CarrotErrorType.BadRequest))]
    [InlineData("LICENSING", nameof(CarrotErrorType.Licensing))]
    [InlineData("UNHANDLED_ERROR", nameof(CarrotErrorType.UnhandledError))]
    public void ErrorResponseAcceptsEveryDocumentedProblemType(
        string wireValue,
        string expectedTypeName)
    {
        #region implementation

        // Arrange
        string errorJson = $$"""
            {
              "type": "{{wireValue}}",
              "message": "Example"
            }
            """;

        // Act
        CarrotErrorResponse? response = JsonSerializer.Deserialize<CarrotErrorResponse>(errorJson);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(expectedTypeName, response!.Type.ToString());

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies the client boundary exposes every query parameter documented for list and cluster.
    /// </summary>
    [Fact]
    public void ApiClientSignaturesExposeTemplateAndIndentQueryParameters()
    {
        #region implementation

        // Arrange
        var listMethod = typeof(ICarrotApiClient).GetMethod(nameof(ICarrotApiClient.GetConfigurationAsync));
        var clusterMethod = typeof(ICarrotApiClient).GetMethod(nameof(ICarrotApiClient.ClusterAsync));

        // Act
        string?[] listParameterNames = listMethod?.GetParameters().Select(parameter => parameter.Name).ToArray()
            ?? Array.Empty<string?>();
        string?[] clusterParameterNames = clusterMethod?.GetParameters().Select(parameter => parameter.Name).ToArray()
            ?? Array.Empty<string?>();

        // Assert
        Assert.Contains("indent", listParameterNames);
        Assert.Contains("template", clusterParameterNames);
        Assert.Contains("indent", clusterParameterNames);
        Assert.Equal(
            typeof(bool?),
            listMethod!.GetParameters().Single(parameter => parameter.Name == "indent").ParameterType);
        Assert.Equal(
            typeof(bool?),
            clusterMethod!.GetParameters().Single(parameter => parameter.Name == "indent").ParameterType);

        #endregion
    }

    #endregion
}
