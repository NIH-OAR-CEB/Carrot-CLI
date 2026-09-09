using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Carrot.Cli.Tests.Configuration;

/**************************************************************/
/// <summary>Verifies binding, ordering, and safe validation of iSearch result configuration.</summary>
public sealed class SearchReturnTypeCatalogTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Ensures shared cardinality names and return-type field order are retained for reporting and requests.</summary>
    [Fact]
    public void GetDefinitions_ValidConfiguration_PreservesOrderAndNames()
    {
        #region implementation

        var catalog = new SearchReturnTypeCatalog(createConfiguration(new Dictionary<string, string?>
        {
            ["iSearchReturnTypes:Results:Cardinality:TotalResultsFieldName"] = "totalCount",
            ["iSearchReturnTypes:Results:Cardinality:CurrentResultsFieldName"] = "returnedCount",
            ["iSearchReturnTypes:Results:Cardinality:PageNumberFieldName"] = "pageNumber",
            ["iSearchReturnTypes:Results:Cardinality:TotalPagesFieldName"] = "totalPages",
            ["iSearchReturnTypes:Results:Grants:DefaultFields:0"] = "grantNumber",
            ["iSearchReturnTypes:Results:Grants:DefaultFields:1"] = "title",
            ["iSearchReturnTypes:Results:Summaries:DefaultFields:0"] = "id"
        }));

        var result = catalog.GetConfiguration();

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal("totalCount", result.Value!.Cardinality.TotalResultsFieldName);
        Assert.Equal("returnedCount", result.Value.Cardinality.CurrentResultsFieldName);
        Assert.Equal("pageNumber", result.Value.Cardinality.PageNumberFieldName);
        Assert.Equal("totalPages", result.Value.Cardinality.TotalPagesFieldName);
        Assert.Collection(
            result.Value.ReturnTypes,
            grants =>
            {
                Assert.Equal("Grants", grants.Name);
                Assert.Equal(["grantNumber", "title"], grants.DefaultFields);
            },
            summaries =>
            {
                Assert.Equal("Summaries", summaries.Name);
                Assert.Equal(["id"], summaries.DefaultFields);
            });

        #endregion
    }

    /**************************************************************/
    /// <summary>Ensures missing and malformed groups are accumulated as safe configuration failures.</summary>
    [Theory]
    [MemberData(nameof(invalidConfigurations))]
    public void GetDefinitions_InvalidConfiguration_ReturnsFailure(
        IReadOnlyDictionary<string, string?> values,
        string expectedCode)
    {
        #region implementation

        var catalog = new SearchReturnTypeCatalog(createConfiguration(values));

        var result = catalog.GetConfiguration();

        Assert.Equal(OperationStatus.Failure, result.Status);
        Assert.Contains(result.Messages, message => message.Code == expectedCode);

        #endregion
    }

    /**************************************************************/
    /// <summary>Supplies representative invalid return-dataset configurations.</summary>
    /// <returns>Invalid configuration values and their expected diagnostic codes.</returns>
    public static IEnumerable<object[]> invalidConfigurations()
    {
        yield return [new Dictionary<string, string?>(), "isearch.return-types.missing"];
        yield return [
            new Dictionary<string, string?>
            {
                ["iSearchReturnTypes:Results:Cardinality:TotalResultsFieldName"] = "totalCount",
                ["iSearchReturnTypes:Results:Cardinality:CurrentResultsFieldName"] = "returnedCount",
                ["iSearchReturnTypes:Results:Cardinality:PageNumberFieldName"] = "pageNumber",
                ["iSearchReturnTypes:Results:Cardinality:TotalPagesFieldName"] = "totalPages",
                ["iSearchReturnTypes:Results:Grants:DefaultFields:0"] = " "
            },
            "isearch.return-type.field-empty"];
        yield return [
            new Dictionary<string, string?>
            {
                ["iSearchReturnTypes:Results:Cardinality:TotalResultsFieldName"] = "totalCount",
                ["iSearchReturnTypes:Results:Cardinality:CurrentResultsFieldName"] = "returnedCount",
                ["iSearchReturnTypes:Results:Cardinality:PageNumberFieldName"] = "pageNumber",
                ["iSearchReturnTypes:Results:Cardinality:TotalPagesFieldName"] = "totalPages",
                ["iSearchReturnTypes:Results:Grants:DefaultFields:0"] = "title",
                ["iSearchReturnTypes:Results:Grants:DefaultFields:1"] = "title"
            },
            "isearch.return-type.field-duplicate"];
        yield return [
            new Dictionary<string, string?>
            {
                ["iSearchReturnTypes:Results:Cardinality:TotalResultsFieldName"] = "totalCount",
                ["iSearchReturnTypes:Results:Cardinality:CurrentResultsFieldName"] = "returnedCount",
                ["iSearchReturnTypes:Results:Cardinality:PageNumberFieldName"] = "pageNumber",
                ["iSearchReturnTypes:Results:Cardinality:TotalPagesFieldName"] = "totalPages",
                ["iSearchReturnTypes:Results:Grants:OtherSetting"] = "ignored"
            },
            "isearch.return-type.fields-missing"];
        yield return [
            new Dictionary<string, string?>
            {
                ["iSearchReturnTypes:Results:Cardinality:CurrentResultsFieldName"] = "returnedCount",
                ["iSearchReturnTypes:Results:Cardinality:PageNumberFieldName"] = "pageNumber",
                ["iSearchReturnTypes:Results:Cardinality:TotalPagesFieldName"] = "totalPages",
                ["iSearchReturnTypes:Results:Grants:DefaultFields:0"] = "title"
            },
            "isearch.cardinality.field-name-missing"];
        yield return [
            new Dictionary<string, string?>
            {
                ["iSearchReturnTypes:Results:Cardinality:TotalResultsFieldName"] = "count",
                ["iSearchReturnTypes:Results:Cardinality:CurrentResultsFieldName"] = "count",
                ["iSearchReturnTypes:Results:Cardinality:PageNumberFieldName"] = "pageNumber",
                ["iSearchReturnTypes:Results:Cardinality:TotalPagesFieldName"] = "totalPages",
                ["iSearchReturnTypes:Results:Grants:DefaultFields:0"] = "title"
            },
            "isearch.cardinality.field-name-duplicate"];
    }

    /**************************************************************/
    /// <summary>Creates isolated configuration from key/value pairs.</summary>
    /// <param name="values">The configuration entries.</param>
    /// <returns>The built configuration.</returns>
    private static IConfiguration createConfiguration(IReadOnlyDictionary<string, string?> values)
    {
        #region implementation

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        #endregion
    }

    #endregion
}
