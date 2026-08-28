using Spectre.Console.Cli;

namespace Carrot.Cli.Cli;

/**************************************************************/
/// <summary>
/// Creates and configures the Spectre command application from the host service provider.
/// </summary>
/// <seealso cref="DependencyInjection.TypeRegistrar"/>
internal sealed class CommandAppFactory
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Creates the command application and defines all user-facing commands and examples.
    /// </summary>
    /// <param name="serviceProvider">The fully built Generic Host service provider.</param>
    /// <returns>A configured Spectre command application.</returns>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    /// <seealso cref="Commands.ProcessCommand"/>
    /// <seealso cref="Commands.PreviewCommand"/>
    internal CommandApp Create(IServiceProvider serviceProvider)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
