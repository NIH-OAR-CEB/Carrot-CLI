namespace Carrot.Cli.Common;

/**************************************************************/
/// <summary>
/// Carries an immutable value, status, and diagnostic messages for expected operation outcomes.
/// </summary>
/// <typeparam name="T">The value type available on successful or partially successful outcomes.</typeparam>
/// <remarks>
/// Expected configuration and file failures use this type rather than exception-based control flow.
/// Infrastructure faults remain exceptional and are translated at the workflow boundary.
/// </remarks>
/// <seealso cref="OperationMessage"/>
/// <seealso cref="OperationStatus"/>
internal sealed record OperationResult<T>
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the aggregate success, partial-success, or failure status.</summary>
    public OperationStatus Status { get; init; }

    /**************************************************************/
    /// <summary>Gets the operation value when one is available.</summary>
    public T? Value { get; init; }

    /**************************************************************/
    /// <summary>Gets immutable diagnostic messages in production order.</summary>
    public IReadOnlyList<OperationMessage> Messages { get; init; } = Array.Empty<OperationMessage>();

    /**************************************************************/
    /// <summary>
    /// Creates a successful result with an optional immutable informational message collection.
    /// </summary>
    /// <param name="value">The successful operation value.</param>
    /// <param name="messages">Optional informational messages.</param>
    /// <returns>A successful operation result.</returns>
    internal static OperationResult<T> Success(T value, IReadOnlyList<OperationMessage>? messages = null)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(value);

        return new OperationResult<T>
        {
            Status = OperationStatus.Success,
            Value = value,
            Messages = copyMessages(messages)
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Creates a partially successful result retaining a usable value and warning messages.
    /// </summary>
    /// <param name="value">The usable partial operation value.</param>
    /// <param name="messages">The immutable warning and informational messages.</param>
    /// <returns>A partially successful operation result.</returns>
    internal static OperationResult<T> PartialSuccess(T value, IReadOnlyList<OperationMessage> messages)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(messages);

        if (messages.Count == 0)
        {
            throw new ArgumentException("Partial success requires at least one diagnostic message.", nameof(messages));
        }

        return new OperationResult<T>
        {
            Status = OperationStatus.PartialSuccess,
            Value = value,
            Messages = copyMessages(messages)
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Creates a failed result without a usable value.
    /// </summary>
    /// <param name="messages">The immutable failure messages.</param>
    /// <returns>A failed operation result.</returns>
    internal static OperationResult<T> Failure(IReadOnlyList<OperationMessage> messages)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(messages);

        if (messages.Count == 0)
        {
            throw new ArgumentException("Failure requires at least one diagnostic message.", nameof(messages));
        }

        return new OperationResult<T>
        {
            Status = OperationStatus.Failure,
            Value = default,
            Messages = copyMessages(messages)
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Creates a failed result that retains diagnostic output which is not eligible for downstream processing.
    /// </summary>
    /// <param name="value">The diagnostic value retained for review.</param>
    /// <param name="messages">The immutable failure messages.</param>
    /// <returns>A failed operation result retaining its review-only value.</returns>
    internal static OperationResult<T> Failure(T value, IReadOnlyList<OperationMessage> messages)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(messages);
        if (messages.Count == 0)
        {
            throw new ArgumentException("Failure requires at least one diagnostic message.", nameof(messages));
        }

        return new OperationResult<T>
        {
            Status = OperationStatus.Failure,
            Value = value,
            Messages = copyMessages(messages)
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Copies a caller-owned message collection into an immutable-by-convention array.
    /// </summary>
    /// <param name="messages">The optional messages supplied by the operation.</param>
    /// <returns>A detached read-only message collection.</returns>
    private static IReadOnlyList<OperationMessage> copyMessages(IReadOnlyList<OperationMessage>? messages)
    {
        #region implementation

        return messages is null || messages.Count == 0
            ? Array.Empty<OperationMessage>()
            : Array.AsReadOnly(messages.ToArray());

        #endregion
    }

    #endregion
}
