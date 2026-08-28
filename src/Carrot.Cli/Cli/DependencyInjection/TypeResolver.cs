using Spectre.Console.Cli;

namespace Carrot.Cli.Cli.DependencyInjection;

/**************************************************************/
/// <summary>
/// Resolves Spectre command types from the Microsoft service provider and owns its scope.
/// </summary>
/// <seealso cref="TypeRegistrar"/>
internal sealed class TypeResolver : ITypeResolver
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Initializes the resolver with the completed application service provider.
    /// </summary>
    /// <param name="provider">The provider used for command dependency resolution.</param>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    internal TypeResolver(IServiceProvider provider)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Resolves an optional command or command dependency type.
    /// </summary>
    /// <param name="type">The type Spectre requests, or <see langword="null"/>.</param>
    /// <returns>The resolved instance, or <see langword="null"/> when no type was requested.</returns>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    public object? Resolve(Type? type)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Releases provider-owned command resources after dispatch completes.
    /// </summary>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    public void Dispose()
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
