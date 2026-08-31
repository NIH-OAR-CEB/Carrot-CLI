namespace Carrot.Cli.Processing;

/**************************************************************/
/// <summary>Carries one retained in-memory prepared batch and its validated Carrot endpoint.</summary>
/// <remarks>
/// The interactive workflow supplies the complete prepared batch so processing never rereads
/// source files or changes the document ordering previewed by the operator.
/// </remarks>
/// <seealso cref="IPreparedDocumentProcessor.ProcessAsync"/>
/// <seealso cref="PreparedDocumentBatch"/>
internal sealed record ProcessPreparedItemsRequest
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the normalized absolute Carrot service endpoint ending in <c>/service</c>.</summary>
    public required Uri Endpoint { get; init; }

    /**************************************************************/
    /// <summary>Gets the retained batch whose ready documents are submitted in exact order.</summary>
    public required PreparedDocumentBatch PreparedBatch { get; init; }

    /**************************************************************/
    /// <summary>Gets the overall time budget applied independently to each Carrot API operation.</summary>
    public TimeSpan Timeout { get; init; }

    #endregion
}
