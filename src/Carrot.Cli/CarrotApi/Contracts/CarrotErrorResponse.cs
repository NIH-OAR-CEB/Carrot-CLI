using System.Text.Json.Serialization;

namespace Carrot.Cli.CarrotApi.Contracts;

/**************************************************************/
/// <summary>
/// Models the Carrot OpenAPI error response without exposing stack details to normal output.
/// </summary>
internal sealed record CarrotErrorResponse
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the problem type such as BAD_REQUEST, LICENSING, or UNHANDLED_ERROR.</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /**************************************************************/
    /// <summary>Gets the human-readable problem description.</summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /**************************************************************/
    /// <summary>Gets the optional server-side exception class for diagnostic logging.</summary>
    [JsonPropertyName("exception")]
    public string? Exception { get; init; }

    /**************************************************************/
    /// <summary>Gets the optional server-side stack trace for protected diagnostic logging.</summary>
    [JsonPropertyName("stacktrace")]
    public string? StackTrace { get; init; }

    #endregion
}
