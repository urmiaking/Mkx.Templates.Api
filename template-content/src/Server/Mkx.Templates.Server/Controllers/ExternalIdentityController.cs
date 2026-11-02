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
        var clientId = User.FindFirstValue("client_id");
        if (string.IsNullOrWhiteSpace(subject) && string.IsNullOrWhiteSpace(clientId))
            return Forbid();

        return Ok(new { subject, clientId, issuer = User.FindFirstValue("iss"), authentication = "external-oidc" });
    }
}
