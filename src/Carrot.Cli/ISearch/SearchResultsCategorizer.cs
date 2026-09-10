using System.Text.Json;
using Carrot.Cli.CarrotApi;
using Carrot.Cli.CarrotApi.Contracts;
using Carrot.Cli.Common;

namespace Carrot.Cli.ISearch;

/**************************************************************/
/// <summary>Maps loaded iSearch records and delegates their categorization to shared Carrot services.</summary>
/// <remarks>
/// This adapter owns the iSearch field contract and result projection. It never performs HTTP
/// directly, so local validation happens before the shared Carrot operation can contact the server.
/// </remarks>
/// <seealso cref="ISearchResultsCategorizer"/>
/// <seealso cref="ICarrotCategorizer"/>
internal sealed class SearchResultsCategorizer : ISearchResultsCategorizer
{
    #region implementation

    private const string NihApplIdField = "nihApplId";
    private const string TitleField = "title";
    private const string AbstractField = "abstract";
    private const string SpecificAimsField = "specificAims";
    private readonly ICarrotCategorizer _carrotCategorizer;

    /**************************************************************/
    /// <summary>Initializes the iSearch adapter with the shared Carrot categorizer.</summary>
    /// <param name="carrotCategorizer">The list-first Carrot execution boundary.</param>
    public SearchResultsCategorizer(ICarrotCategorizer carrotCategorizer)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(carrotCategorizer);
        _carrotCategorizer = carrotCategorizer;

        #endregion
    }

    /**************************************************************/
    /// <summary>Submits the current session's loaded records to Carrot and correlates memberships.</summary>
    /// <param name="session">The current iSearch session and loaded result pages.</param>
    /// <param name="endpoint">The normalized Carrot service endpoint.</param>
    /// <param name="timeout">The timeout applied to each Carrot operation.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>The correlated categorized batch or a structured expected failure.</returns>
    public async Task<OperationResult<CategorizedISearchResultBatch>> CategorizeAsync(
        SearchResultPageSession session,
        Uri endpoint,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(endpoint);
        if (session.WalkedResults.Count == 0)
        {
            return failure<CategorizedISearchResultBatch>(
                "isearch.categorization.no-results",
                "No loaded iSearch records are available for categorization.");
        }

        var mapped = mapRecords(session);
        if (mapped.Value is not { } mappedRecords)
        {
            return OperationResult<CategorizedISearchResultBatch>.Failure(mapped.Messages);
        }

        var carrotResult = await _carrotCategorizer.CategorizeAsync(
            new CarrotCategorizationRequest
            {
                Endpoint = endpoint,
                Documents = mappedRecords.Documents,
                Timeout = timeout
            },
            cancellationToken).ConfigureAwait(false);
        if (carrotResult.Value is not { } categorized)
        {
            return OperationResult<CategorizedISearchResultBatch>.Failure(carrotResult.Messages);
        }

        var rows = mappedRecords.Rows
            .Select((row, index) => row with
            {
                Memberships = categorized.MembershipsByDocument[index]
            })
            .ToArray();

        return OperationResult<CategorizedISearchResultBatch>.Success(new CategorizedISearchResultBatch
        {
            RunId = categorized.RunId,
            Endpoint = categorized.Endpoint,
            Request = categorized.Request,
            Configuration = categorized.Configuration,
            Response = categorized.Response,
            Rows = Array.AsReadOnly(rows)
        });

        #endregion
    }

    /**************************************************************/
    /// <summary>Maps all retained pages into the exact four-field Carrot document contract.</summary>
    /// <param name="session">The session containing retained pages and generic JSON records.</param>
    /// <returns>Mapped documents and source rows, or the first safe mapping failure.</returns>
    private static OperationResult<MappedRecords> mapRecords(SearchResultPageSession session)
    {
        #region implementation

        var documents = new List<ClusterDocument>(session.WalkedResults.Count);
        var rows = new List<CategorizedISearchResultRow>(session.WalkedResults.Count);
        var resultOrdinal = 0;

        // Enumerate pages rather than only the flattened list so the workbook can retain service-page provenance.
        foreach (var page in session.WalkedPages)
        {
            for (var pageOrdinal = 0; pageOrdinal < page.Results.Count; pageOrdinal++)
            {
                var record = page.Results[pageOrdinal];
                if (record.ValueKind != JsonValueKind.Object)
                {
                    return failure<MappedRecords>(
                        "isearch.categorization.record-object",
                        $"Loaded iSearch result {resultOrdinal:N0} is not a JSON object.");
                }

                var fieldResult = readFields(record, resultOrdinal);
                if (fieldResult.Value is not { } fields)
                {
                    return OperationResult<MappedRecords>.Failure(fieldResult.Messages);
                }

                documents.Add(new ClusterDocument
                {
                    // Carrot accepts document values as strings or string arrays. Normalize source
                    // scalar values at this boundary while the categorized row retains the original JSON value.
                    AdditionalFields = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                    {
                        [NihApplIdField] = toCarrotText(fields.NihApplId),
                        [TitleField] = toCarrotText(fields.Title),
                        [AbstractField] = toCarrotText(fields.Abstract),
                        [SpecificAimsField] = toCarrotText(fields.SpecificAims)
                    }
                });
                rows.Add(new CategorizedISearchResultRow
                {
                    ResultPage = page.Cardinality.PageNumber,
                    ResultOrdinal = resultOrdinal,
                    NihApplId = fields.NihApplId,
                    Title = fields.Title,
                    Abstract = fields.Abstract,
                    SpecificAims = fields.SpecificAims
                });
                resultOrdinal++;
            }
        }

        return OperationResult<MappedRecords>.Success(new MappedRecords
        {
            Documents = Array.AsReadOnly(documents.ToArray()),
            Rows = Array.AsReadOnly(rows.ToArray())
        });

        #endregion
    }

    /**************************************************************/
    /// <summary>Converts one validated iSearch scalar into a Carrot-compatible string value.</summary>
    /// <param name="value">The detached iSearch scalar.</param>
    /// <returns>A JSON string preserving text or the invariant numeric representation.</returns>
    /// <remarks>
    /// Carrot's document contract does not accept JSON numbers or null field values. Empty text is
    /// used for a source null so all four selected field names remain present in the request, while
    /// the original value remains available on the categorized result row and export.
    /// </remarks>
    private static JsonElement toCarrotText(JsonElement value)
    {
        #region implementation

        var text = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.Null => string.Empty,
            _ => throw new ArgumentException("The iSearch value is not a supported scalar.", nameof(value))
        };

        return JsonSerializer.SerializeToElement(text);

        #endregion
    }

    /**************************************************************/
    /// <summary>Reads and validates the four selected fields from one result object.</summary>
    /// <param name="record">The generic iSearch result object.</param>
    /// <param name="resultOrdinal">The zero-based loaded-record ordinal for diagnostics.</param>
    /// <returns>Detached field values or a safe mapping failure.</returns>
    /// <remarks>
    /// An absent selected property is represented as a source null. The request mapper later
    /// emits an empty Carrot string for that value so one incomplete record does not reject the
    /// complete categorization request.
    /// </remarks>
    private static OperationResult<MappedFields> readFields(JsonElement record, int resultOrdinal)
    {
        #region implementation

        var fields = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var fieldName in new[] { NihApplIdField, TitleField, AbstractField, SpecificAimsField })
        {
            if (!record.TryGetProperty(fieldName, out var value))
            {
                // Carrot requires a string value for every emitted field. Keep the source-side
                // absence as null and let the request mapper emit a valid empty string.
                fields[fieldName] = JsonSerializer.SerializeToElement<string?>(null);
                continue;
            }

            if (value.ValueKind is JsonValueKind.Object or JsonValueKind.Array
                || (fieldName != NihApplIdField
                    && value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
                || (fieldName == NihApplIdField
                    && value.ValueKind is not (JsonValueKind.String or JsonValueKind.Number or JsonValueKind.Null)))
            {
                return failure<MappedFields>(
                    "isearch.categorization.field-type",
                    $"Loaded iSearch result {resultOrdinal:N0} has an unsupported value for selected field '{fieldName}'.");
            }

            fields[fieldName] = value.Clone();
        }

        return OperationResult<MappedFields>.Success(new MappedFields
        {
            NihApplId = fields[NihApplIdField],
            Title = fields[TitleField],
            Abstract = fields[AbstractField],
            SpecificAims = fields[SpecificAimsField]
        });

        #endregion
    }

    /**************************************************************/
    /// <summary>Creates one typed expected mapping failure.</summary>
    /// <typeparam name="T">The failed operation value type.</typeparam>
    /// <param name="code">The stable diagnostic code.</param>
    /// <param name="message">The safe operator-facing message.</param>
    /// <returns>A failed operation result.</returns>
    private static OperationResult<T> failure<T>(string code, string message)
    {
        #region implementation

        return OperationResult<T>.Failure(
        [new OperationMessage { Code = code, Message = message, Severity = OperationMessageSeverity.Error }]);

        #endregion
    }

    /**************************************************************/
    /// <summary>Contains the mapped Carrot documents and their source rows before submission.</summary>
    private sealed record MappedRecords
    {
        #region implementation

        /**************************************************************/
        /// <summary>Gets the exact Carrot documents aligned to the source rows.</summary>
        public IReadOnlyList<ClusterDocument> Documents { get; init; } = Array.Empty<ClusterDocument>();

        /**************************************************************/
        /// <summary>Gets the source rows retained for post-response projection.</summary>
        public IReadOnlyList<CategorizedISearchResultRow> Rows { get; init; }
            = Array.Empty<CategorizedISearchResultRow>();

        #endregion
    }

    /**************************************************************/
    /// <summary>Contains detached values for the required iSearch result fields.</summary>
    private sealed record MappedFields
    {
        #region implementation

        /**************************************************************/
        /// <summary>Gets the detached NIH application identifier value.</summary>
        public required JsonElement NihApplId { get; init; }

        /**************************************************************/
        /// <summary>Gets the detached title value.</summary>
        public required JsonElement Title { get; init; }

        /**************************************************************/
        /// <summary>Gets the detached abstract value.</summary>
        public required JsonElement Abstract { get; init; }

        /**************************************************************/
        /// <summary>Gets the detached specific-aims value.</summary>
        public required JsonElement SpecificAims { get; init; }

        #endregion
    }

    #endregion
}
