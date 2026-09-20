using System.ComponentModel.DataAnnotations;

namespace Trellis.Api.Core.Configuration;

/// <summary>
/// App-wide, not module-specific — <see cref="BaseUrl"/> is the frontend origin every module's
/// outbound links (password reset, tenant data-export ready, a future Leasing application link)
/// get built against. Bound from a top-level <c>AppBaseUrl</c> env var, not
/// <c>Modules__{X}__*</c>, since it isn't owned by any one module.
/// </summary>
public class AppOptions
{
    [Required]
    [Url]
    public string BaseUrl { get; init; } = "http://localhost:5173";
}
