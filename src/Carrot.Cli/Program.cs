namespace Carrot.Cli;

/**************************************************************/
/// <summary>
/// Provides the process entry point for the Carrot command-line application.
/// </summary>
/// <remarks>
/// The future implementation will create the Generic Host, configure logging and
/// dependency injection, build the Spectre command application, and dispatch arguments.
/// </remarks>
/// <seealso cref="Composition.ServiceRegistration"/>
/// <seealso cref="Cli.CommandAppFactory"/>
public static class Program
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Starts the command-line application with the supplied process arguments.
    /// </summary>
    /// <param name="args">The raw command-line arguments supplied by the operating system.</param>
    /// <returns>A task whose result is one of the documented process exit codes.</returns>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    /// <seealso cref="Processing.ExitCodes"/>
    public static Task<int> Main(string[] args)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
