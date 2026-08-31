using Carrot.Cli.CarrotApi;
using Carrot.Cli.Common;
using Xunit;

namespace Carrot.Cli.Tests.CarrotApi;

/**************************************************************/
/// <summary>Verifies strict Carrot service endpoint validation and normalization.</summary>
public sealed class EndpointResolverTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Verifies absolute HTTP and HTTPS endpoints accept one optional trailing slash.</summary>
    /// <param name="endpoint">The valid candidate endpoint.</param>
    /// <param name="expected">The normalized endpoint.</param>
    [Theory]
    [InlineData("http://localhost:8080/service", "http://localhost:8080/service")]
    [InlineData("https://carrot.example/service/", "https://carrot.example/service")]
    [InlineData("https://carrot.example/deployment/service/", "https://carrot.example/deployment/service")]
    public void Resolve_ValidServiceEndpoint_ReturnsNormalizedUri(string endpoint, string expected)
    {
        #region implementation

        // Act
        var result = new EndpointResolver().Resolve(endpoint);

        // Assert
        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(expected, result.Value!.AbsoluteUri.TrimEnd('/'));

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies unsupported schemes, paths, credentials, queries, fragments, and empty values fail.</summary>
    /// <param name="endpoint">The invalid candidate endpoint.</param>
    /// <param name="expectedCode">The expected stable validation code.</param>
    [Theory]
    [InlineData(null, "endpoint.empty")]
    [InlineData("relative/service", "endpoint.invalid")]
    [InlineData("ftp://localhost/service", "endpoint.invalid")]
    [InlineData("http://user:password@localhost/service", "endpoint.credentials")]
    [InlineData("http://localhost/service?indent=true", "endpoint.query")]
    [InlineData("http://localhost/service#section", "endpoint.fragment")]
    [InlineData("http://localhost/service/list", "endpoint.path")]
    public void Resolve_InvalidEndpoint_ReturnsStructuredFailure(string? endpoint, string expectedCode)
    {
        #region implementation

        // Act
        var result = new EndpointResolver().Resolve(endpoint);

        // Assert
        Assert.Equal(OperationStatus.Failure, result.Status);
        Assert.Null(result.Value);
        Assert.Equal(expectedCode, Assert.Single(result.Messages).Code);

        #endregion
    }

    #endregion
}
