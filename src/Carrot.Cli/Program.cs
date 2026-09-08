using Carrot.Cli.Cli;
using Carrot.Cli.Composition;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Carrot.Cli;

/**************************************************************/
/// <summary>
/// Provides the process entry point for the Carrot command-line application.
/// </summary>
/// <remarks>
/// Creates the Generic Host service collection, configures dependency injection, builds
/// the Spectre command application, and dispatches interactive or named command arguments.
/// </remarks>
/// <seealso cref="Composition.ServiceRegistration"/>
/// <seealso cref="Cli.CommandAppFactory"/>
public class Program
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Starts the command-line application with the supplied process arguments.
    /// </summary>
    /// <param name="args">The raw command-line arguments supplied by the operating system.</param>
    /// <returns>A task whose result is one of the documented process exit codes.</returns>
    /// <seealso cref="Processing.ExitCodes"/>
    public static async Task<int> Main(string[] args)
    {
        #region implementation

        var builder = Host.CreateApplicationBuilder(args);

        // Load the configured local credentials even when the debugger does not set Development.
        builder.Configuration.AddUserSecrets<Program>(optional: true);

        builder.Services.AddCarrotCli(builder.Configuration);

        // Spectre owns construction and disposal of the service provider through its registrar.
        var application = new CommandAppFactory().Create(builder.Services);
        return await application.RunAsync(args).ConfigureAwait(false);

        #endregion
    }

    #endregion
}
