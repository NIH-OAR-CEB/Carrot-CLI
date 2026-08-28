using System.Text;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>
/// Reads runtime help from Markdown resources embedded in the Carrot CLI assembly.
/// </summary>
/// <remarks>
/// Embedded resources make help independent of the process working directory and ensure
/// self-contained folder deployments retain the same curated content as local builds.
/// </remarks>
/// <seealso cref="IHelpContentProvider"/>
internal sealed class EmbeddedHelpContentProvider : IHelpContentProvider
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Reads all UTF-8 Markdown text associated with the supplied topic.
    /// </summary>
    /// <param name="topic">The resolved topic containing the manifest-resource name.</param>
    /// <returns>The complete Markdown document, or <see langword="null"/> when it is not embedded.</returns>
    public string? Read(HelpTopic topic)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(topic);

        using var stream = typeof(EmbeddedHelpContentProvider).Assembly
            .GetManifestResourceStream(topic.ResourceName);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: false);
        return reader.ReadToEnd();

        #endregion
    }

    #endregion
}
