using Carrot.Cli.Cli.UI;
using Carrot.Cli.Cli.Reporting;
using Carrot.Cli.Cli.Commands;
using Carrot.Cli.CarrotApi;
using Carrot.Cli.Configuration;
using Carrot.Cli.Common;
using Carrot.Cli.Extraction;
using Carrot.Cli.Extraction.Extractors;
using Carrot.Cli.Input;
using Carrot.Cli.ISearch;
using Carrot.Cli.Processing;
using Carrot.Cli.Reporting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Carrot.Cli.Composition;

/**************************************************************/
/// <summary>
/// Defines the composition-root registration boundary for all Carrot CLI features.
/// </summary>
/// <remarks>
/// Interactive preparation, reusable clustering configuration, in-memory Carrot processing,
/// atomic JSON persistence, named request preview/process, explicit processed-result Excel export,
/// and optional iSearch registrations are active.
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
        services.AddSingleton(configuration);
        services.AddOptions<CarrotCliOptions>()
            .Bind(configuration.GetSection("CarrotCli"))
            .ValidateOnStart();
        services.AddOptions<ISearchOptions>()
            .Bind(configuration.GetSection("iSearch"));

        // iSearch credentials remain optional at startup; the feature validates them on entry.
        services.AddSingleton<ISearchOptionsValidator>();
        services.AddSingleton<SearchReturnTypeCatalog>();

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
        services.AddTransient<IDocumentProcessingWorkflow, DocumentProcessingWorkflow>();
        services.AddSingleton<ClusterRequestFactory>();
        services.AddSingleton<ClusteringConfigurationResolver>();
        services.AddSingleton<ClusteringConfigurationValidator>();
        services.AddSingleton<EndpointResolver>();
        services.AddSingleton<RunSettingsResolver>();
        services.AddSingleton<ClusterMembershipMapper>();
        services.AddSingleton<IRunIdProvider, SystemRunIdProvider>();
        services.AddTransient<ICarrotCategorizer, CarrotCategorizer>();
        services.AddTransient<IPreparedDocumentProcessor, PreparedDocumentProcessor>();
        services.AddHttpClient<ICarrotApiClient, CarrotApiClient>(client =>
            {
                // CarrotApiClient owns one overall timeout across retries.
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                // Never forward complete extracted document content to a redirect target.
                AllowAutoRedirect = false
            });

        // Keep iSearch isolated from the Carrot client because its host, authentication, and contracts differ.
        services.AddHttpClient<IISearchApiClient, ISearchApiClient>(client =>
            {
                client.BaseAddress = new Uri("https://isearch.opa-tools.od.nih.gov/api/", UriKind.Absolute);
                client.Timeout = Timeout.InfiniteTimeSpan;
                client.DefaultRequestHeaders.Accept.Add(
                    new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                // Never forward the authenticated cookie to an unexpected redirect target.
                AllowAutoRedirect = false
            });

        services.AddSingleton<HelpTopicCatalog>();
        services.AddSingleton<IHelpContentProvider, EmbeddedHelpContentProvider>();
        services.AddSingleton<IApplicationPreambleProvider, FileApplicationPreambleProvider>();
        services.AddTransient<MarkdownHelpRenderer>();
        services.AddTransient<ApplicationPreambleRenderer>();
        services.AddTransient<ApplicationFooterRenderer>();
        services.AddTransient<HelpRenderer>();
        services.AddTransient<AboutRenderer>();
        services.AddTransient<ConsoleReporter>();
        services.AddTransient<ServerInformationPager>();
        services.AddTransient<ServerInfoCommand>();
        services.AddTransient<PreviewCommand>();
        services.AddTransient<ProcessCommand>();
        services.AddTransient<ISearchCommand>();
        services.AddTransient<ISearchCommandWorkflow, SearchCommandWorkflow>();
        services.AddTransient<ServerInformationFlow>();
        services.AddTransient<ISearchResultsCategorizer, SearchResultsCategorizer>();
        services.AddTransient<ISearchResultsCategorizationFlow, SearchResultsCategorizationFlow>();
        services.AddTransient<ISearchResultsPager, SearchResultsPager>();
        services.AddTransient<ICategorizedISearchResultsPager, CategorizedISearchResultsPager>();
        services.AddTransient<ICategorizedISearchResultsExportFlow, CategorizedISearchResultsExportFlow>();
        services.AddTransient<ISearchFieldsPager, SearchFieldsPager>();
        services.AddTransient<IAdvancedISearchQueryBuilder, AdvancedISearchQueryBuilder>();
        services.AddTransient<ISearchFlow, InteractiveISearchFlow>();
        services.AddTransient<InteractivePreviewFlow>();
        services.AddSingleton<ICommandRunLogger, CommandRunLogger>();
        services.AddTransient<PreparedResultsPager>();
        services.AddTransient<PreparedJsonPackagePager>();
        services.AddTransient<ProcessedResultsPager>();
        services.AddSingleton<AtomicFileWriter>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<ExcelOutputPathResolver>();
        services.AddSingleton<ExcelOutputPathSuggester>();
        services.AddSingleton<ProcessedDocumentReportMapper>();
        services.AddSingleton<ExcelReportWriter>();
        services.AddSingleton<IExcelReportWriter>(provider => provider.GetRequiredService<ExcelReportWriter>());
        services.AddSingleton<IExcelWorkbookWriter>(provider => provider.GetRequiredService<ExcelReportWriter>());
        services.AddSingleton<IJsonArtifactWriter, JsonArtifactWriter>();
        services.AddTransient<IProcessedResultsExporter, ProcessedResultsExporter>();
        services.AddTransient<ProcessedResultsExportFlow>();
        services.AddSingleton<SearchResultsReportMapper>();
        services.AddTransient<ISearchResultsExporter, SearchResultsExporter>();
        services.AddTransient<ISearchResultsExportFlow, SearchResultsExportFlow>();
        services.AddSingleton<CategorizedISearchResultsReportMapper>();
        services.AddTransient<ICategorizedISearchResultsExporter, CategorizedISearchResultsExporter>();
        services.AddTransient<ProcessDocumentsMenu>();
        services.AddTransient<InteractiveMenu>();

        return services;

        #endregion
    }

    #endregion
}
