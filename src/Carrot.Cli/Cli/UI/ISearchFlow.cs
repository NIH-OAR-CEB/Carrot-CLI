namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>Defines the interactive iSearch availability, discovery, and query workflow.</summary>
/// <remarks>The implementation returns to its owner after expected failures; caller cancellation remains cooperative.</remarks>
/// <seealso cref="InteractiveISearchFlow"/>
internal interface ISearchFlow
{
    /**************************************************************/
    /// <summary>Runs one iSearch visit and returns to the owning main menu.</summary>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task representing the iSearch workflow.</returns>
    Task RunAsync(CancellationToken cancellationToken);
}
