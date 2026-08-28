using Carrot.Cli.Processing;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>
/// Coordinates prompt-driven endpoint selection and workflow execution for human users.
/// </summary>
/// <remarks>
/// The endpoint will be prompted on every interactive session, prefilled with
/// <c>http://localhost:8080/service</c>, and never persisted.
/// </remarks>
/// <seealso cref="IDocumentProcessingWorkflow"/>
internal sealed class InteractiveMenu
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Initializes the menu with processing and console presentation boundaries.
    /// </summary>
    /// <param name="workflow">The document processing workflow.</param>
    /// <param name="reporter">The console reporter.</param>
    /// <param name="helpRenderer">The help-topic renderer.</param>
    /// <param name="aboutRenderer">The application-information renderer.</param>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    internal InteractiveMenu(
        IDocumentProcessingWorkflow workflow,
        ConsoleReporter reporter,
        HelpRenderer helpRenderer,
        AboutRenderer aboutRenderer)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Displays the menu, collects one run's values, and dispatches the selected action.
    /// </summary>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task containing the selected action's process exit code.</returns>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    internal Task<int> RunAsync(CancellationToken cancellationToken)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
