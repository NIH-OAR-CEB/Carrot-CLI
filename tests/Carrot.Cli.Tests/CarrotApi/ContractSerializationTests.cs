using Xunit;

namespace Carrot.Cli.Tests.CarrotApi;

/**************************************************************/
/// <summary>
/// Reserves future contract coverage against the local Carrot 4.8.6 OpenAPI document.
/// </summary>
public sealed class ContractSerializationTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Verifies ordered title/content documents and nested parameter JSON match the request schema.</summary>
    [Fact(Skip = "Future acceptance: request serialization is layout-only.")]
    public void ClusterRequestMatchesTheAuthoritativeOpenApiContract()
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies list, recursive cluster, and error responses retain all schema fields.</summary>
    [Fact(Skip = "Future acceptance: response deserialization is layout-only.")]
    public void ResponsesMatchTheAuthoritativeOpenApiContract()
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
