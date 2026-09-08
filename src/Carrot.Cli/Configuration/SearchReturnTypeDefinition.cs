namespace Carrot.Cli.Configuration;

/**************************************************************/
/// <summary>
/// Describes one operator-selectable iSearch return dataset and its ordered result fields.
/// </summary>
/// <remarks>
/// The definition is loaded from one child of <c>iSearchReturnTypes</c>. The name is a display
/// label and the fields are sent to iSearch as the selected result-field set.
/// </remarks>
/// <seealso cref="SearchReturnTypeCatalog"/>
internal sealed class SearchReturnTypeDefinition
{
    #region implementation

    /**************************************************************/
    /// <summary>Gets the configured display label for this return dataset.</summary>
    /// <remarks>The label is the configuration child key, such as <c>Grants</c>.</remarks>
    public string Name { get; init; } = string.Empty;

    /**************************************************************/
    /// <summary>Gets the ordered iSearch field names returned for this dataset.</summary>
    /// <remarks>Field names preserve configuration order and are sent unchanged to the search boundary.</remarks>
    public IReadOnlyList<string> DefaultFields { get; init; } = Array.Empty<string>();

    #endregion
}
