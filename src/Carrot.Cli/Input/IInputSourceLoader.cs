using Carrot.Cli.Common;

namespace Carrot.Cli.Input;

/**************************************************************/
/// <summary>
/// Defines loading and safe discovery for one supported direct-file, folder, or ZIP input kind.
/// </summary>
/// <seealso cref="FolderInputSourceLoader"/>
/// <seealso cref="FileInputSourceLoader"/>
/// <seealso cref="ZipInputSourceLoader"/>
internal interface IInputSourceLoader
{
    /**************************************************************/
    /// <summary>
    /// Loads a direct file, folder, or ZIP container and returns its deterministic source-file batch.
    /// </summary>
    /// <param name="inputPath">The source container path.</param>
    /// <param name="recursive">Whether folder discovery includes subdirectories.</param>
    /// <param name="cancellationToken">The token signaling cooperative cancellation.</param>
    /// <returns>A task containing an input batch or structured expected input failures.</returns>
    Task<OperationResult<InputBatch>> LoadAsync(
        string inputPath,
        bool recursive,
        CancellationToken cancellationToken);
}
