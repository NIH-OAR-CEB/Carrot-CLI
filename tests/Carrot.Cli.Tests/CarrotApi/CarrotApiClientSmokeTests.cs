using Carrot.Cli.CarrotApi;
using Carrot.Cli.CarrotApi.Contracts;
using Carrot.Cli.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Carrot.Cli.Tests.CarrotApi;

/**************************************************************/
/// <summary>Provides an explicit opt-in smoke test for a developer-run local Carrot 4.8.6 service.</summary>
public sealed class CarrotApiClientSmokeTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Calls local <c>/list</c> and <c>/cluster</c> without participating in normal automation.</summary>
    /// <remarks>
    /// Start Carrot 4.8.6 at <c>http://localhost:8080/service</c>, then run the test executable
    /// with <c>-explicit on</c> and a filter for this class. Normal builds do not execute it.
    /// </remarks>
    [Fact(Explicit = true)]
    public async Task LocalCarrot486_ListAndCluster_ReturnValidResponses()
    {
        #region implementation

        // Arrange
        using var httpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        var client = new CarrotApiClient(
            httpClient,
            NullLogger<CarrotApiClient>.Instance,
            Options.Create(new CarrotCliOptions()));
        var endpoint = new Uri("http://localhost:8080/service");

        // Act
        var listResult = await client.GetConfigurationAsync(
            endpoint,
            TimeSpan.FromSeconds(120),
            indent: null,
            TestContext.Current.CancellationToken);
        var clusterResult = await client.ClusterAsync(
            endpoint,
            new ClusterRequest
            {
                Algorithm = "Lingo",
                Language = "English",
                Documents =
                [
                    new ClusterDocument { Title = "Carrots", Content = "Carrots are orange root vegetables." },
                    new ClusterDocument { Title = "Potatoes", Content = "Potatoes are starchy root vegetables." }
                ]
            },
            template: null,
            TimeSpan.FromSeconds(120),
            indent: null,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(listResult.Value);
        Assert.NotNull(clusterResult.Value);

        #endregion
    }

    #endregion
}
