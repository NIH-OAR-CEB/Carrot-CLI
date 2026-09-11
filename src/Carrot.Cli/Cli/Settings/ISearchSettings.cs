using System.ComponentModel;
using System.Globalization;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Carrot.Cli.Cli.Settings;

/**************************************************************/
/// <summary>Defines prompt-free iSearch search, advanced-query, paging, categorization, and export options.</summary>
/// <remarks>
/// Repeated query-field and filter-query options preserve command-line order. The settings layer
/// validates only values and relationships that do not require live iSearch state; the workflow
/// validates databases and field names after authenticated discovery.
/// </remarks>
/// <seealso cref="EndpointSettings"/>
internal sealed class ISearchSettings : EndpointSettings
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets or initializes the live iSearch database name.</summary>
    [CommandOption("--database <NAME>", isRequired: true)]
    [Description("Live iSearch database returned by GET /datasets.")]
    public string? Database { get; init; }

    /**************************************************************/
    /// <summary>Gets or initializes the configured return-dataset name used to select result fields.</summary>
    [CommandOption("--result-dataset <NAME>", isRequired: true)]
    [Description("Configured iSearch return dataset whose DefaultFields become fl.")]
    public string? ResultDataset { get; init; }

    /**************************************************************/
    /// <summary>Gets or initializes the base iSearch query, defaulting to the match-all query.</summary>
    [CommandOption("--query <TEXT>")]
    [Description("Base iSearch query q; defaults to *:* when omitted.")]
    public string Query { get; init; } = "*:*";

    /**************************************************************/
    /// <summary>Gets or initializes ordered live fields used for unqualified query terms.</summary>
    [CommandOption("--query-field <FIELD>")]
    [Description("Repeat for qf fields used by unqualified query terms.")]
    public string[] QueryFields { get; init; } = [];

    /**************************************************************/
    /// <summary>Gets or initializes ordered field-qualified filter expressions.</summary>
    [CommandOption("--filter-query <EXPRESSION>")]
    [Description("Repeat for fq filter expressions such as fy:2024.")]
    public string[] FilterQueries { get; init; } = [];

    /**************************************************************/
    /// <summary>Gets or initializes the default Boolean operator for the search.</summary>
    [CommandOption("--default-op <AND|OR>")]
    [Description("Advanced default operator; defaults to AND.")]
    public string DefaultOp { get; init; } = "AND";

    /**************************************************************/
    /// <summary>Gets or initializes the number of records requested per iSearch page.</summary>
    [CommandOption("--rows <COUNT>")]
    [Description("Rows per iSearch request from 1 through 100; defaults to 100.")]
    public int Rows { get; init; } = 100;

    /**************************************************************/
    /// <summary>Gets or initializes the lower inclusive service update-date bound.</summary>
    [CommandOption("--updated-after <YYYY-MM-DD>")]
    [Description("Optional updatedAfter date in yyyy-MM-dd form.")]
    public string? UpdatedAfter { get; init; }

    /**************************************************************/
    /// <summary>Gets or initializes the upper inclusive service update-date bound.</summary>
    [CommandOption("--updated-before <YYYY-MM-DD>")]
    [Description("Optional updatedBefore date in yyyy-MM-dd form.")]
    public string? UpdatedBefore { get; init; }

    /**************************************************************/
    /// <summary>Gets or initializes the maximum number of retained records for a bounded walk.</summary>
    [CommandOption("--max-results <COUNT>")]
    [Description("Retained-record bound; defaults to 100 unless --all-results is supplied.")]
    public int? MaxResults { get; init; }

    /**************************************************************/
    /// <summary>Gets or initializes whether every service-reported result should be walked.</summary>
    [CommandOption("--all-results")]
    [Description("Walk every cursor page until iSearch totalCount is accepted.")]
    public bool AllResults { get; init; }

    /**************************************************************/
    /// <summary>Gets or initializes the optional original-result workbook destination.</summary>
    [CommandOption("--output <PATH>")]
    [Description("Optional original iSearch .xlsx output path.")]
    public string? OutputPath { get; init; }

    /**************************************************************/
    /// <summary>Gets or initializes whether retained records should be sent to Carrot.</summary>
    [CommandOption("--categorize")]
    [Description("Categorize loaded records through the shared Carrot path.")]
    public bool Categorize { get; init; }

    /**************************************************************/
    /// <summary>Gets or initializes the optional categorized workbook destination.</summary>
    [CommandOption("--categorized-output <PATH>")]
    [Description("Optional categorized .xlsx output path; requires --categorize.")]
    public string? CategorizedOutputPath { get; init; }

    /**************************************************************/
    /// <summary>Gets or initializes whether existing workbooks may be replaced.</summary>
    [CommandOption("--overwrite")]
    [Description("Permit replacement of existing requested workbooks.")]
    public bool Overwrite { get; init; }

    /**************************************************************/
    /// <summary>Gets or initializes whether the normal success summary is suppressed.</summary>
    [CommandOption("--quiet")]
    [Description("Suppress the normal success summary while retaining warnings and errors.")]
    public bool Quiet { get; init; }

    /**************************************************************/
    /// <summary>Validates option relationships that do not require live service state.</summary>
    /// <returns>A successful validation result or one actionable command-line error.</returns>
    public override ValidationResult Validate()
    {
        #region implementation

        if (string.IsNullOrWhiteSpace(Database))
        {
            return ValidationResult.Error("Supply --database.");
        }

        if (string.IsNullOrWhiteSpace(ResultDataset))
        {
            return ValidationResult.Error("Supply --result-dataset.");
        }

        if (string.IsNullOrWhiteSpace(Query))
        {
            return ValidationResult.Error("--query must not be blank when supplied.");
        }

        if (AllResults && MaxResults.HasValue)
        {
            return ValidationResult.Error("--all-results cannot be combined with --max-results.");
        }

        if (MaxResults is <= 0)
        {
            return ValidationResult.Error("--max-results must be greater than zero.");
        }

        if (Rows is < 1 or > 100)
        {
            return ValidationResult.Error("--rows must be between 1 and 100.");
        }

        if (!string.Equals(DefaultOp, "AND", StringComparison.Ordinal)
            && !string.Equals(DefaultOp, "OR", StringComparison.Ordinal))
        {
            return ValidationResult.Error("--default-op must be AND or OR.");
        }

        if (!isValidDate(UpdatedAfter) || !isValidDate(UpdatedBefore))
        {
            return ValidationResult.Error("Update dates must use yyyy-MM-dd.");
        }

        if (UpdatedAfter is not null
            && UpdatedBefore is not null
            && string.CompareOrdinal(UpdatedAfter, UpdatedBefore) > 0)
        {
            return ValidationResult.Error("--updated-after cannot be later than --updated-before.");
        }

        if (CategorizedOutputPath is not null && !Categorize)
        {
            return ValidationResult.Error("--categorized-output requires --categorize.");
        }

        return ValidationResult.Success();

        #endregion
    }

    /**************************************************************/
    /// <summary>Checks an optional date using the exact service calendar representation.</summary>
    /// <param name="value">The optional date text.</param>
    /// <returns><see langword="true"/> when the value is absent or exact yyyy-MM-dd.</returns>
    private static bool isValidDate(string? value)
    {
        #region implementation

        return value is null
            || DateOnly.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _);

        #endregion
    }

    #endregion
}
