using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Leasing.Application.Dtos;
using Trellis.Api.Modules.Leasing.Application.Interfaces;

namespace Trellis.Api.Modules.Leasing.Presentation.Controllers;

/// <summary>
/// Outside the standard JWT scheme entirely — authenticated by token validity, not a signature or
/// a session (leasing_design.md). Every failure mode (unknown, expired, already-consumed token, a
/// tenant that isn't entitled, a listing no longer accepting applications) is a plain 404, never
/// a 403: no existence disclosure.
/// </summary>
[ApiController]
[AllowAnonymous]
[ApiExplorerSettings(GroupName = "leasing")]
[Route("api/v1/public/application-links")]
public sealed class PublicApplicationsController(IApplicationLinkService linkService, IApplicationService applicationService) : ControllerBase
{
    /// <summary>Resolving the emailed link IS BDR-LSE-008's email-control verification. Repeatable until the application is submitted.</summary>
    [HttpGet("{token}")]
    [ProducesResponseType(typeof(ApiResponse<ApplicationLinkContextResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Resolve(string token)
    {
        var result = await linkService.ResolveAsync(token);
        return result.Outcome == ServiceResultOutcome.Success
            ? ServiceResult<ApplicationLinkContextResponse>.Success(new ApplicationLinkContextResponse(
                result.Data!.VacancyListingId, result.Data.UnitLabel, result.Data.RentAmount, result.Data.Currency, result.Data.ApplicantEmail))
                .ToActionResult(HttpContext)
            : ServiceResult<ApplicationLinkContextResponse>.NotFound().ToActionResult(HttpContext);
    }

    [HttpPost("{token}/applications")]
    [ProducesResponseType(typeof(ApiResponse<ApplicationSubmitResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Submit(string token, [FromBody] ApplicationSubmitRequest request) =>
        (await applicationService.SubmitAsync(token, request)).ToActionResult(HttpContext);
}
