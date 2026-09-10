using Carrot.Cli.Common;

namespace Carrot.Cli.CarrotApi;

/**************************************************************/
/// <summary>Defines the shared list-first Carrot categorization boundary.</summary>
/// <remarks>
/// File and iSearch adapters use this contract so endpoint validation, configuration resolution,
/// HTTP execution, response validation, membership mapping, and run identity cannot diverge.
/// </remarks>
/// <seealso cref="CarrotCategorizer"/>
/// <seealso cref="CarrotCategorizationRequest"/>
internal interface ICarrotCategorizer
{
    /**************************************************************/
    /// <summary>Validates and submits one ordered document collection to Carrot.</summary>
    /// <param name="request">The endpoint, documents, clustering selection, and timeout.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>The correlated category result or a structured expected failure.</returns>
    Task<OperationResult<CarrotCategorizationResult>> CategorizeAsync(
        CarrotCategorizationRequest request,
        CancellationToken cancellationToken);
}
