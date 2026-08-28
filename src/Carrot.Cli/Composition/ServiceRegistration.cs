using Carrot.Cli.Cli.UI;
using Carrot.Cli.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Carrot.Cli.Composition;

/**************************************************************/
/// <summary>
/// Defines the composition-root registration boundary for all Carrot CLI features.
/// </summary>
/// <remarks>
/// UI registrations are active. Operational workflow, HTTP, extraction, and reporting
/// registrations remain deferred until their implementations replace the layout stubs.
/// </remarks>
/// <seealso cref="CarrotCliOptions"/>
internal static class ServiceRegistration
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Adds the implemented interactive-menu, help, and application-information services.
    /// </summary>
    /// <param name="services">The service collection owned by the Generic Host.</param>
    /// <param name="configuration">The layered application configuration.</param>
    /// <returns>The supplied service collection for fluent registration.</returns>
    /// <seealso cref="CarrotCliOptionsValidator"/>
    internal static IServiceCollection AddCarrotCli(this IServiceCollection services, IConfiguration configuration)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<HelpTopicCatalog>();
        services.AddSingleton<IHelpContentProvider, EmbeddedHelpContentProvider>();
        services.AddSingleton<IApplicationPreambleProvider, FileApplicationPreambleProvider>();
        services.AddTransient<MarkdownHelpRenderer>();
        services.AddTransient<ApplicationPreambleRenderer>();
        services.AddTransient<HelpRenderer>();
        services.AddTransient<AboutRenderer>();
        services.AddTransient<InteractiveMenu>();

        return services;

        #endregion
    }

    #endregion
}
