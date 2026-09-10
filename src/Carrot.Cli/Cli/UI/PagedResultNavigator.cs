using Spectre.Console;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>Provides shared bounded next/previous/save/back navigation for result pages.</summary>
/// <remarks>
/// The navigator owns only page state and keyboard choices. Each result view supplies its own page
/// renderer and save callback, so source-specific tables and export contracts remain separate.
/// </remarks>
internal static class PagedResultNavigator
{
    #region implementation

    /**************************************************************/
    /// <summary>Displays pages until the supplied save or back action returns.</summary>
    /// <param name="console">The console receiving pages and navigation prompts.</param>
    /// <param name="itemCount">The number of rows available to page.</param>
    /// <param name="pageSize">The positive number of rows shown on each page.</param>
    /// <param name="title">The prompt title without page numbering.</param>
    /// <param name="saveLabel">The operator-facing save action label.</param>
    /// <param name="backLabel">The operator-facing back action label.</param>
    /// <param name="selectSaveByDefaultOnFinalPage">Whether Save is first on the final page.</param>
    /// <param name="renderPage">The source-specific page renderer.</param>
    /// <param name="saveAsync">The source-specific asynchronous save callback.</param>
    /// <param name="cancellationToken">The token signaling console cancellation.</param>
    /// <returns>A task representing result-page navigation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a reference argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a text argument is blank or a size is invalid.</exception>
    internal static async Task ShowAsync(
        IAnsiConsole console,
        int itemCount,
        int pageSize,
        string title,
        string saveLabel,
        string backLabel,
        bool selectSaveByDefaultOnFinalPage,
        Action<int, int> renderPage,
        Func<CancellationToken, Task> saveAsync,
        CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(console);
        ArgumentOutOfRangeException.ThrowIfNegative(itemCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(saveLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(backLabel);
        ArgumentNullException.ThrowIfNull(renderPage);
        ArgumentNullException.ThrowIfNull(saveAsync);

        var pageCount = Math.Max(1, (itemCount + pageSize - 1) / pageSize);
        var pageIndex = 0;

        while (true)
        {
            renderPage(pageIndex, pageCount);
            var choices = createChoices(pageIndex, pageCount, selectSaveByDefaultOnFinalPage);
            var selected = await new SelectionPrompt<PageChoice>()
                .Title($"[bold orange1]{Markup.Escape(title)}[/] — Page {pageIndex + 1} of {pageCount}")
                .HighlightStyle(new Style(Color.Black, Color.Orange1))
                .UseConverter(choice => getChoiceLabel(choice, saveLabel, backLabel))
                .AddChoices(choices)
                .AddCancelResult(PageChoice.Back)
                .ShowAsync(console, cancellationToken)
                .ConfigureAwait(false);

            switch (selected)
            {
                case PageChoice.Next:
                    pageIndex++;
                    break;
                case PageChoice.Previous:
                    pageIndex--;
                    break;
                case PageChoice.Save:
                    await saveAsync(cancellationToken).ConfigureAwait(false);
                    break;
                case PageChoice.Back:
                    return;
                default:
                    throw new InvalidOperationException($"Unsupported result-page action: {selected}");
            }
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>Builds navigation choices while preserving each view's final-page default policy.</summary>
    /// <param name="pageIndex">The zero-based current page.</param>
    /// <param name="pageCount">The total page count.</param>
    /// <param name="selectSaveByDefaultOnFinalPage">Whether Save should be the first final-page choice.</param>
    /// <returns>The valid ordered navigation choices.</returns>
    private static IReadOnlyList<PageChoice> createChoices(
        int pageIndex,
        int pageCount,
        bool selectSaveByDefaultOnFinalPage)
    {
        #region implementation

        var choices = new List<PageChoice>();
        if (pageIndex + 1 < pageCount)
        {
            choices.Add(PageChoice.Next);
        }
        else if (selectSaveByDefaultOnFinalPage)
        {
            choices.Add(PageChoice.Save);
        }

        if (pageIndex > 0)
        {
            choices.Add(PageChoice.Previous);
        }

        if (!choices.Contains(PageChoice.Save))
        {
            choices.Add(PageChoice.Save);
        }

        choices.Add(PageChoice.Back);
        return choices;

        #endregion
    }

    /**************************************************************/
    /// <summary>Maps shared navigation choices to source-specific operator labels.</summary>
    /// <param name="choice">The shared navigation choice.</param>
    /// <param name="saveLabel">The source-specific save label.</param>
    /// <param name="backLabel">The source-specific back label.</param>
    /// <returns>The operator-facing label.</returns>
    private static string getChoiceLabel(PageChoice choice, string saveLabel, string backLabel) => choice switch
    {
        PageChoice.Next => "Next Page",
        PageChoice.Previous => "Previous Page",
        PageChoice.Save => saveLabel,
        PageChoice.Back => backLabel,
        _ => choice.ToString()
    };

    /**************************************************************/
    /// <summary>Defines the navigation states shared by retained result views.</summary>
    private enum PageChoice
    {
        /**************************************************************/
        /// <summary>Displays the following page.</summary>
        Next,

        /**************************************************************/
        /// <summary>Displays the preceding page.</summary>
        Previous,

        /**************************************************************/
        /// <summary>Runs the source-specific save callback.</summary>
        Save,

        /**************************************************************/
        /// <summary>Returns to the owning workflow.</summary>
        Back
    }

    #endregion
}
