using Microsoft.Extensions.Options;

namespace Carrot.Cli.Configuration;

/**************************************************************/
/// <summary>
/// Validates configured safety limits and defaults before the host accepts commands.
/// </summary>
/// <seealso cref="CarrotCliOptions"/>
internal sealed class CarrotCliOptionsValidator : IValidateOptions<CarrotCliOptions>
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Validates one named or default options instance and accumulates all configuration failures.
    /// </summary>
    /// <param name="name">The optional named-options key.</param>
    /// <param name="options">The bound options instance.</param>
    /// <returns>A success result or all validation errors.</returns>
    /// <exception cref="NotImplementedException">Always thrown by the layout-only scaffold.</exception>
    public ValidateOptionsResult Validate(string? name, CarrotCliOptions options)
    {
        #region implementation

        throw new NotImplementedException("Layout stub only.");

        #endregion
    }

    #endregion
}
