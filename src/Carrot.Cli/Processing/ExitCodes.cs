namespace Carrot.Cli.Processing;

/**************************************************************/
/// <summary>
/// Defines stable process exit codes for automation and Windows Task Scheduler.
/// </summary>
internal static class ExitCodes
{
    #region implementation

    /**************************************************************/
    /// <summary>Indicates complete success.</summary>
    internal const int Success = 0;

    /**************************************************************/
    /// <summary>Indicates invalid command syntax or configuration.</summary>
    internal const int InvalidConfiguration = 1;

    /**************************************************************/
    /// <summary>Indicates useful output with one or more file-level failures.</summary>
    internal const int PartialSuccess = 2;

    /**************************************************************/
    /// <summary>Indicates input failure or no processable documents.</summary>
    internal const int InputFailure = 3;

    /**************************************************************/
    /// <summary>Indicates endpoint validation or list-request failure.</summary>
    internal const int EndpointFailure = 4;

    /**************************************************************/
    /// <summary>Indicates cluster-request or response-contract failure.</summary>
    internal const int ClusterFailure = 5;

    /**************************************************************/
    /// <summary>Indicates report or artifact persistence failure.</summary>
    internal const int OutputFailure = 6;

    /**************************************************************/
    /// <summary>Indicates iSearch health, discovery, search, or cursor failure.</summary>
    internal const int ISearchFailure = 7;

    /**************************************************************/
    /// <summary>Indicates cooperative cancellation.</summary>
    internal const int Cancellation = 130;

    #endregion
}
