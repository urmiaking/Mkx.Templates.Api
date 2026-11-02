using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mkx.Templates.Server.Controllers;

/// <summary>Shows the subject of a validated external JWT; does not create local users.</summary>
[ApiController]
[Route("api/auth/external")]
[Authorize(Policy = "ExternalOidcOnly")]
public sealed class ExternalIdentityController : ControllerBase
{
    [HttpGet("me")]
    public IActionResult Me()
    {
        var subject = User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(subject))
            return Forbid();

        return Ok(new { subject, issuer = User.FindFirstValue("iss"), authentication = "external-oidc" });
    }
}
