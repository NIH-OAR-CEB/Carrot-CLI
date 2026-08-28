using System.Text.Json.Serialization;

namespace Carrot.Cli.CarrotApi.Contracts;

/**************************************************************/
/// <summary>
/// Defines the problem types enumerated by the Carrot 4.8.6 OpenAPI error schema.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<CarrotErrorType>))]
internal enum CarrotErrorType
{
    /**************************************************************/
    /// <summary>Indicates an invalid or incomplete client request.</summary>
    [JsonStringEnumMemberName("BAD_REQUEST")]
    BadRequest,

    /**************************************************************/
    /// <summary>Indicates a server licensing problem.</summary>
    [JsonStringEnumMemberName("LICENSING")]
    Licensing,

    /**************************************************************/
    /// <summary>Indicates an unhandled server-side failure.</summary>
    [JsonStringEnumMemberName("UNHANDLED_ERROR")]
    UnhandledError
}

/**************************************************************/
/// <summary>
/// Models the Carrot OpenAPI error response without exposing stack details to normal output.
/// </summary>
internal sealed record CarrotErrorResponse
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the OpenAPI-enumerated problem type.</summary>
    [JsonPropertyName("type")]
    public required CarrotErrorType Type { get; init; }

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
