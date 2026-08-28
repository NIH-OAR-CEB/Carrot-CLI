using Spectre.Console.Cli;
using Microsoft.Extensions.DependencyInjection;

namespace Carrot.Cli.Cli.DependencyInjection;

/**************************************************************/
/// <summary>
/// Adapts the Microsoft dependency-injection container to Spectre command construction.
/// </summary>
/// <seealso cref="TypeResolver"/>
internal sealed class TypeRegistrar : ITypeRegistrar
{
    #region implementation

    private readonly IServiceCollection _services;

    /**************************************************************/
    /// <summary>
    /// Initializes an adapter over the application's service collection.
    /// </summary>
    /// <param name="services">The mutable service collection used to register commands.</param>
    internal TypeRegistrar(IServiceCollection services)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(services);
        _services = services;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Builds the final Spectre type resolver.
    /// </summary>
    /// <returns>A resolver backed by the completed service provider.</returns>
    public ITypeResolver Build()
    {
        #region implementation

        return new TypeResolver(_services.BuildServiceProvider());

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Registers a service-to-implementation mapping requested by Spectre.
    /// </summary>
    /// <param name="service">The service contract type.</param>
    /// <param name="implementation">The implementation type.</param>
    public void Register(Type service, Type implementation)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(implementation);
        _services.AddSingleton(service, implementation);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Registers an existing service instance requested by Spectre.
    /// </summary>
    /// <param name="service">The service contract type.</param>
    /// <param name="implementation">The preconstructed implementation instance.</param>
    public void RegisterInstance(Type service, object implementation)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(implementation);
        _services.AddSingleton(service, implementation);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Registers a lazily created service instance requested by Spectre.
    /// </summary>
    /// <param name="service">The service contract type.</param>
    /// <param name="factory">The factory that creates the implementation.</param>
    public void RegisterLazy(Type service, Func<object> factory)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(factory);
        _services.AddSingleton(service, _ => factory());

        #endregion
    }

    #endregion
}
