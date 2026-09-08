namespace Carrot.Cli.Configuration;

/**************************************************************/
/// <summary>
/// Contains optional iSearch connection settings loaded from the <c>iSearch</c> configuration section.
/// </summary>
/// <remarks>
/// Credentials are intentionally not validated during host startup because iSearch is an optional
/// interactive feature. Validation occurs immediately before the feature sends a request, allowing
/// all other CLI commands to run without iSearch configuration.
/// </remarks>
/// <seealso cref="ISearchOptionsValidator"/>
internal sealed class ISearchOptions
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets or sets the iSearch API key sent as the <c>apiKey</c> cookie.</summary>
    /// <remarks>The value is never included in a URL, diagnostic message, or log entry.</remarks>
    public string? ApiKey { get; set; }

    /**************************************************************/
    /// <summary>Gets or sets the monitored contact address sent as the HTTP <c>From</c> header.</summary>
    /// <remarks>The value is required only when an iSearch operation is invoked.</remarks>
    public string? ContactEmail { get; set; }

    /**************************************************************/
    /// <summary>Gets or sets the overall timeout, in seconds, for one iSearch operation.</summary>
    /// <remarks>The timeout is shared by all attempts for the operation.</remarks>
    public int TimeoutSeconds { get; set; } = 120;

    /**************************************************************/
    /// <summary>Gets or sets the maximum number of transient retries for one operation.</summary>
    /// <remarks>Authentication, validation, redirect, and malformed-response failures are not retried.</remarks>
    public int TransientRetryCount { get; set; } = 2;

    /**************************************************************/
    /// <summary>Gets or sets the minimum delay between sequential authenticated requests, in milliseconds.</summary>
    /// <remarks>The first request is immediate; subsequent requests share one process-wide pace.</remarks>
    public int MinimumRequestIntervalMilliseconds { get; set; } = 1_000;

    /**************************************************************/
    /// <summary>Gets or sets the maximum response size for ordinary iSearch operations in characters.</summary>
    /// <remarks>Health, dataset, and search payloads exceeding this limit are rejected before display.</remarks>
    public int MaximumResponseCharacters { get; set; } = 8_192;

    /**************************************************************/
    /// <summary>Gets or sets the maximum field-discovery response size in characters.</summary>
    /// <remarks>
    /// Field schemas can contain many definitions, so this separate bound is larger than the
    /// general response limit while still preventing an unbounded successful payload.
    /// </remarks>
    public int MaximumFieldsResponseCharacters { get; set; } = 1_048_576;

    #endregion
}
