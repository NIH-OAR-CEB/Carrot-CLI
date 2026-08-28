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

    private readonly IServiceProvider _provider;

    /**************************************************************/
    /// <summary>
    /// Initializes the resolver with the completed application service provider.
    /// </summary>
    /// <param name="provider">The provider used for command dependency resolution.</param>
    internal TypeResolver(IServiceProvider provider)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(provider);
        _provider = provider;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Resolves an optional command or command dependency type.
    /// </summary>
    /// <param name="type">The type Spectre requests, or <see langword="null"/>.</param>
    /// <returns>The resolved instance, or <see langword="null"/> when no type was requested.</returns>
    public object? Resolve(Type? type)
    {
        #region implementation

        return type is null ? null : _provider.GetService(type);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Releases provider-owned command resources after dispatch completes.
    /// </summary>
    public void Dispose()
    {
        #region implementation

        if (_provider is IDisposable disposableProvider)
        {
            disposableProvider.Dispose();
        }

        #endregion
    }

    #endregion
}
