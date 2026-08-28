using System.Reflection;
using Carrot.Cli.Cli.Commands;
using Carrot.Cli.Cli.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
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
    /// <param name="services">The mutable service collection shared with Spectre's registrar.</param>
    /// <returns>A configured Spectre command application.</returns>
    /// <seealso cref="Commands.ProcessCommand"/>
    /// <seealso cref="Commands.PreviewCommand"/>
    internal CommandApp Create(IServiceCollection services)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(services);

        var application = new CommandApp(new TypeRegistrar(services));
        application.SetDefaultCommand<InteractiveCommand>();
        application.Configure(Configure);

        return application;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Applies application identity and the complete named-command metadata to a Spectre configurator.
    /// </summary>
    /// <param name="configuration">The command configurator owned by Spectre or its test harness.</param>
    /// <remarks>
    /// Keeping configuration in one method lets command metadata tests exercise the exact production
    /// registrations through <c>CommandAppTester</c> without duplicating command names or descriptions.
    /// </remarks>
    internal void Configure(IConfigurator configuration)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(configuration);

        configuration.SetApplicationName("carrot-cli");
        configuration.SetApplicationVersion(getApplicationVersion());

        configuration.AddCommand<ProcessCommand>("process")
            .WithDescription("Cluster documents and create report artifacts.");
        configuration.AddCommand<PreviewCommand>("preview")
            .WithDescription("Preview the request without submitting it for clustering.");
        configuration.AddCommand<ServerInfoCommand>("server-info")
            .WithDescription("Display algorithms, languages, and templates from the server.");
        configuration.AddCommand<HelpCommand>("help")
            .WithDescription("Display curated Markdown help for an optional topic.");
        configuration.AddCommand<AboutCommand>("about")
            .WithDescription("Display application, compatibility, and notice information.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Reads the informational version used by Spectre's built-in version option.
    /// </summary>
    /// <returns>The informational version without build metadata when available.</returns>
    /// <seealso cref="AssemblyInformationalVersionAttribute"/>
    private static string getApplicationVersion()
    {
        #region implementation

        var assembly = typeof(Program).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion.Split('+', 2)[0];
        }

        return assembly.GetName().Version?.ToString() ?? "0.0.0";

        #endregion
    }

    #endregion
}
