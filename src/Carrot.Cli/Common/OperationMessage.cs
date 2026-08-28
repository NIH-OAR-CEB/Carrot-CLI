namespace Carrot.Cli.Common;

/**************************************************************/
/// <summary>
/// Defines the diagnostic severity of one immutable operation message.
/// </summary>
internal enum OperationMessageSeverity
{
    /**************************************************************/
    /// <summary>Indicates contextual information.</summary>
    Information,

    /**************************************************************/
    /// <summary>Indicates a nonfatal condition requiring attention.</summary>
    Warning,

    /**************************************************************/
    /// <summary>Indicates a failed operation or failed file.</summary>
    Error
}

/**************************************************************/
/// <summary>
/// Carries one immutable coded information, warning, or error message.
/// </summary>
internal sealed record OperationMessage
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets a stable machine-readable message code.</summary>
    public required string Code { get; init; }

    /**************************************************************/
    /// <summary>Gets the human-readable message text.</summary>
    public required string Message { get; init; }

    /**************************************************************/
    /// <summary>Gets the diagnostic severity that contributes to the aggregate status.</summary>
    public OperationMessageSeverity Severity { get; init; }

    #endregion
}
