using Carrot.Cli.Common;
using Carrot.Cli.CarrotApi;
using Spectre.Console;

namespace Carrot.Cli.Cli.UI;

/**************************************************************/
/// <summary>Provides the shared interactive Carrot endpoint prompt and validation.</summary>
/// <remarks>The default endpoint and URI validation remain identical across document and iSearch workflows.</remarks>
/// <seealso cref="EndpointResolver"/>
internal static class CarrotEndpointPrompt
{
    /**************************************************************/
    /// <summary>Prompts for a validated Carrot service endpoint.</summary>
    /// <param name="console">The interactive console.</param>
    /// <param name="endpointResolver">The shared endpoint URI validator.</param>
    /// <param name="prompt">The operator-facing prompt text.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>The validated endpoint URI.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    internal static async Task<Uri> PromptAsync(
        IAnsiConsole console,
        EndpointResolver endpointResolver,
        string prompt,
        CancellationToken cancellationToken)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(endpointResolver);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        var endpointText = await new TextPrompt<string>(prompt)
            .DefaultValue("http://localhost:8080/service")
            .PromptStyle("yellow")
            .Validate(value =>
            {
                var endpointResult = endpointResolver.Resolve(value);
                return endpointResult.Value is null
                    ? ValidationResult.Error(endpointResult.Messages[0].Message)
                    : ValidationResult.Success();
            })
            .ShowAsync(console, cancellationToken)
            .ConfigureAwait(false);

        return endpointResolver.Resolve(endpointText).Value!;

        #endregion
    }
}
