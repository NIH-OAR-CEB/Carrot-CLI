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

    /**************************************************************/
    /// <summary>
    /// Initializes an adapter over the application's service collection.
    /// </summary>
    /// <param name="services">The mutable service collection used to register commands.</param>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    internal TypeRegistrar(IServiceCollection services)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Builds the final Spectre type resolver.
    /// </summary>
    /// <returns>A resolver backed by the completed service provider.</returns>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    public ITypeResolver Build()
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Registers a service-to-implementation mapping requested by Spectre.
    /// </summary>
    /// <param name="service">The service contract type.</param>
    /// <param name="implementation">The implementation type.</param>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    public void Register(Type service, Type implementation)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Registers an existing service instance requested by Spectre.
    /// </summary>
    /// <param name="service">The service contract type.</param>
    /// <param name="implementation">The preconstructed implementation instance.</param>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    public void RegisterInstance(Type service, object implementation)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Registers a lazily created service instance requested by Spectre.
    /// </summary>
    /// <param name="service">The service contract type.</param>
    /// <param name="factory">The factory that creates the implementation.</param>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    public void RegisterLazy(Type service, Func<object> factory)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
