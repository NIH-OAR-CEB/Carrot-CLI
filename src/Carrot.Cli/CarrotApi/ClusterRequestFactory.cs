using Carrot.Cli.CarrotApi.Contracts;
using Carrot.Cli.Configuration;
using Carrot.Cli.Extraction;
using Microsoft.Extensions.Options;

namespace Carrot.Cli.CarrotApi;

/**************************************************************/
/// <summary>Maps prepared documents to the exact Carrot cluster-request wire contract.</summary>
/// <remarks>
/// Interactive JSON preview and future HTTP submission share this factory so document order,
/// complete extracted content, language, and algorithm cannot drift between those paths.
/// Client-only source metadata remains outside the request contract.
/// </remarks>
/// <seealso cref="ClusterRequest"/>
/// <seealso cref="ExtractedDocument"/>
internal sealed class ClusterRequestFactory
{
    #region implementation

    private readonly CarrotCliOptions _options;

    /**************************************************************/
    /// <summary>Initializes the factory with validated default clustering selections.</summary>
    /// <param name="options">The validated application defaults.</param>
    public ClusterRequestFactory(IOptions<CarrotCliOptions> options)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one default request containing every supplied document in prepared order.</summary>
    /// <param name="documents">The successfully extracted documents retained by the prepared batch.</param>
    /// <returns>The complete in-memory request ready for JSON serialization or future submission.</returns>
    /// <remarks>
    /// The mapping deliberately sends only title and full extracted content. Physical paths,
    /// hashes, ordinals, and other local correlation fields must not leak into the Carrot payload.
    /// </remarks>
    /// <seealso cref="ClusterDocument"/>
    internal ClusterRequest Create(IReadOnlyList<ExtractedDocument> documents)
    {
        #region implementation

        return Create(
            documents,
            new ClusteringConfiguration
            {
                Algorithm = _options.DefaultAlgorithm,
                Language = _options.DefaultLanguage
            });

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one request from explicit resolved clustering selections and prepared documents.</summary>
    /// <param name="documents">The successfully extracted documents retained by the prepared batch.</param>
    /// <param name="configuration">The resolved direct or template clustering configuration.</param>
    /// <returns>The complete in-memory request ready for JSON serialization or submission.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="documents"/> or <paramref name="configuration"/> is null.
    /// </exception>
    /// <seealso cref="ClusteringConfiguration"/>
    internal ClusterRequest Create(
        IReadOnlyList<ExtractedDocument> documents,
        ClusteringConfiguration configuration)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(configuration);
        return new ClusterRequest
        {
            Language = configuration.Language,
            Algorithm = configuration.Algorithm,
            Parameters = configuration.Parameters,
            Documents = documents
                .Select(document => new ClusterDocument
                {
                    Title = document.Title,
                    Content = document.Content
                })
                .ToArray()
        };

        #endregion
    }

    #endregion
}
