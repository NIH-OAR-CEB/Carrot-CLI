namespace Carrot.Cli.Common;

/**************************************************************/
/// <summary>
/// Defines complete success, usable partial success, and terminal failure outcomes.
/// </summary>
internal enum OperationStatus
{
    /**************************************************************/
    /// <summary>Indicates completion without warning or error messages.</summary>
    Success,

    /**************************************************************/
    /// <summary>Indicates usable output accompanied by one or more nonfatal file failures.</summary>
    PartialSuccess,

    /**************************************************************/
    /// <summary>Indicates that the requested operation could not produce usable output.</summary>
    Failure
}
