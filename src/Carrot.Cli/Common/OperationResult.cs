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
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    internal static OperationResult<T> Success(T value, IReadOnlyList<OperationMessage>? messages = null)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Creates a partially successful result retaining a usable value and warning messages.
    /// </summary>
    /// <param name="value">The usable partial operation value.</param>
    /// <param name="messages">The immutable warning and informational messages.</param>
    /// <returns>A partially successful operation result.</returns>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    internal static OperationResult<T> PartialSuccess(T value, IReadOnlyList<OperationMessage> messages)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Creates a failed result without a usable value.
    /// </summary>
    /// <param name="messages">The immutable failure messages.</param>
    /// <returns>A failed operation result.</returns>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    internal static OperationResult<T> Failure(IReadOnlyList<OperationMessage> messages)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
