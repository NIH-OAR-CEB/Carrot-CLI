using Carrot.Cli.Cli.UI;
using Carrot.Cli.CarrotApi;
using Carrot.Cli.Configuration;
using Carrot.Cli.Common;
using Carrot.Cli.Extraction;
using Carrot.Cli.Extraction.Extractors;
using Carrot.Cli.Input;
using Carrot.Cli.Processing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Carrot.Cli.Composition;

/**************************************************************/
/// <summary>
/// Defines the composition-root registration boundary for all Carrot CLI features.
/// </summary>
/// <remarks>
/// Interactive preparation registrations are active. Carrot HTTP, clustering, preview,
/// and report persistence registrations remain deferred until their later milestones.
/// </remarks>
/// <seealso cref="CarrotCliOptions"/>
internal static class ServiceRegistration
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Adds validated preparation, input, extraction, interactive-menu, help, and application-information services.
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

        services.AddSingleton<IValidateOptions<CarrotCliOptions>, CarrotCliOptionsValidator>();
        services.AddOptions<CarrotCliOptions>()
            .Bind(configuration.GetSection("CarrotCli"))
            .ValidateOnStart();

        services.AddSingleton<DocumentFormatCatalog>();
        services.AddSingleton<InputPathNormalizer>();
        services.AddSingleton<FolderInputSourceLoader>();
        services.AddSingleton<FileInputSourceLoader>();
        services.AddSingleton<ZipInputSourceLoader>();
        services.AddSingleton<InputSourceResolver>();
        services.AddSingleton<HashService>();
        services.AddSingleton<ExtractionResultFactory>();
        services.AddSingleton<IDocumentTextExtractor, PlainTextExtractor>();
        services.AddSingleton<IDocumentTextExtractor, WordDocumentExtractor>();
        services.AddSingleton<IDocumentTextExtractor, SpreadsheetExtractor>();
        services.AddSingleton<IDocumentTextExtractor, PresentationExtractor>();
        services.AddSingleton<IDocumentTextExtractor, PdfDocumentExtractor>();
        services.AddSingleton<DocumentExtractionCoordinator>();
        services.AddSingleton<IDocumentPreparationWorkflow, DocumentPreparationWorkflow>();
        services.AddSingleton<ClusterRequestFactory>();

        services.AddSingleton<HelpTopicCatalog>();
        services.AddSingleton<IHelpContentProvider, EmbeddedHelpContentProvider>();
        services.AddSingleton<IApplicationPreambleProvider, FileApplicationPreambleProvider>();
        services.AddTransient<MarkdownHelpRenderer>();
        services.AddTransient<ApplicationPreambleRenderer>();
        services.AddTransient<HelpRenderer>();
        services.AddTransient<AboutRenderer>();
        services.AddTransient<PreparedResultsPager>();
        services.AddTransient<PreparedJsonPackageRenderer>();
        services.AddTransient<ProcessDocumentsMenu>();
        services.AddTransient<InteractiveMenu>();

        return services;

        #endregion
    }

    #endregion
}
