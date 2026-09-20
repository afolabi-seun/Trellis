using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trellis.Api.Modules.Auth.Application.Interfaces;

namespace Trellis.Api.Modules.Auth.Presentation.Controllers;

/// <summary>security_guide.md: the RS256 public key, served for any client that wants to verify a Trellis-issued JWT independently.</summary>
[ApiController]
[Route("api/v1/auth/.well-known")]
[ApiExplorerSettings(GroupName = "auth")]
[AllowAnonymous]
public sealed class JwksController(IJwtTokenService jwtTokenService) : ControllerBase
{
    [HttpGet("jwks.json")]
    public ContentResult GetJwks() => Content(jwtTokenService.GetJwksJson(), "application/json");
}
