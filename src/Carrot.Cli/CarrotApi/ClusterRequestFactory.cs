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
    /// <summary>Creates one request containing every supplied document in prepared order.</summary>
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

        ArgumentNullException.ThrowIfNull(documents);
        return new ClusterRequest
        {
            Language = _options.DefaultLanguage,
            Algorithm = _options.DefaultAlgorithm,
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
