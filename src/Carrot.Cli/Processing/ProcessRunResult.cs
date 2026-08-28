using Carrot.Cli.Common;
using Carrot.Cli.Reporting;

namespace Carrot.Cli.Processing;

/**************************************************************/
/// <summary>
/// Describes a completed process or preview run and its generated artifacts and rows.
/// </summary>
/// <seealso cref="OperationStatus"/>
/// <seealso cref="ReportRow"/>
internal sealed record ProcessRunResult
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the unique correlation identifier for the run.</summary>
    public Guid RunId { get; init; }

    /**************************************************************/
    /// <summary>Gets the terminal success, partial-success, or failure state.</summary>
    public OperationStatus Status { get; init; }

    /**************************************************************/
    /// <summary>Gets the documented process exit code.</summary>
    public int ExitCode { get; init; }

    /**************************************************************/
    /// <summary>Gets the immutable informational, warning, and error messages.</summary>
    public IReadOnlyList<OperationMessage> Messages { get; init; } = Array.Empty<OperationMessage>();

    /**************************************************************/
    /// <summary>Gets the report rows retained in deterministic source order.</summary>
    public IReadOnlyList<ReportRow> Rows { get; init; } = Array.Empty<ReportRow>();

    /**************************************************************/
    /// <summary>Gets the paths of artifacts successfully created for the run.</summary>
    public IReadOnlyList<string> ArtifactPaths { get; init; } = Array.Empty<string>();

    #endregion
}
