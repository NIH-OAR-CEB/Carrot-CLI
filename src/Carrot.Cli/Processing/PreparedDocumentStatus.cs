namespace Carrot.Cli.Processing;

/**************************************************************/
/// <summary>Defines whether one prepared-result row is eligible for future processing.</summary>
internal enum PreparedDocumentStatus
{
    /**************************************************************/
    /// <summary>Indicates successful extraction and hashing.</summary>
    Ready,

    /**************************************************************/
    /// <summary>Indicates an expected source or extraction failure.</summary>
    Failed
}
