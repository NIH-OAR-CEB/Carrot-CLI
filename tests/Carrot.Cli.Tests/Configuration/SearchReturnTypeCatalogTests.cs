using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Carrot.Cli.Tests.Configuration;

/**************************************************************/
/// <summary>Verifies binding, ordering, and safe validation of iSearch return datasets.</summary>
public sealed class SearchReturnTypeCatalogTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Ensures configured group and field order are retained for menu and request use.</summary>
    [Fact]
    public void GetDefinitions_ValidConfiguration_PreservesOrderAndNames()
    {
        #region implementation

        var catalog = new SearchReturnTypeCatalog(createConfiguration(new Dictionary<string, string?>
        {
            ["iSearchReturnTypes:Grants:DefaultFields:0"] = "grantNumber",
            ["iSearchReturnTypes:Grants:DefaultFields:1"] = "title",
            ["iSearchReturnTypes:Summaries:DefaultFields:0"] = "id"
        }));

        var result = catalog.GetDefinitions();

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Collection(
            result.Value!,
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

        var result = catalog.GetDefinitions();

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
                ["iSearchReturnTypes:Grants:DefaultFields:0"] = " "
            },
            "isearch.return-type.field-empty"];
        yield return [
            new Dictionary<string, string?>
            {
                ["iSearchReturnTypes:Grants:DefaultFields:0"] = "title",
                ["iSearchReturnTypes:Grants:DefaultFields:1"] = "title"
            },
            "isearch.return-type.field-duplicate"];
        yield return [
            new Dictionary<string, string?>
            {
                ["iSearchReturnTypes:Grants:OtherSetting"] = "ignored"
            },
            "isearch.return-type.fields-missing"];
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
