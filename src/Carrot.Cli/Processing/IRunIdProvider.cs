namespace Carrot.Cli.Processing;

/**************************************************************/
/// <summary>
/// Creates correlation identifiers for complete successful processing runs.
/// </summary>
/// <remarks>
/// The abstraction keeps volatile identifier generation replaceable in deterministic tests.
/// A run identifier is requested only after Carrot response validation and correlation succeed.
/// </remarks>
/// <seealso cref="ProcessedDocumentBatch"/>
internal interface IRunIdProvider
{
    /**************************************************************/
    /// <summary>Creates one nonempty identifier for a successful processed batch.</summary>
    /// <returns>A new run correlation identifier.</returns>
    Guid Create();
}
