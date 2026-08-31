using Carrot.Cli.Common;

namespace Carrot.Cli.Reporting;

/**************************************************************/
/// <summary>
/// Defines explicit persistence of one retained successful processed result to Excel.
/// </summary>
/// <seealso cref="ProcessedResultsExporter"/>
internal interface IProcessedResultsExporter
{
    /**************************************************************/
    /// <summary>Maps and saves one processed result without contacting Carrot or rereading source files.</summary>
    /// <param name="request">The retained batch, normalized output path, and overwrite policy.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>A successful absolute path or a structured expected write failure.</returns>
    Task<OperationResult<string>> SaveAsync(
        SaveProcessedResultsRequest request,
        CancellationToken cancellationToken);
}
