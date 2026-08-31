namespace Carrot.Cli.Processing;

/**************************************************************/
/// <summary>
/// Creates cryptographically strong platform GUIDs for successful processing runs.
/// </summary>
/// <seealso cref="IRunIdProvider"/>
internal sealed class SystemRunIdProvider : IRunIdProvider
{
    #region implementation

    /**************************************************************/
    /// <summary>Creates one nonempty identifier for a successful processed batch.</summary>
    /// <returns>A new run correlation identifier.</returns>
    public Guid Create()
    {
        #region implementation

        return Guid.NewGuid();

        #endregion
    }

    #endregion
}
