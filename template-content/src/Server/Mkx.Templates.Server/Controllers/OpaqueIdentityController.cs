using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mkx.Templates.Server.Auth;

namespace Mkx.Templates.Server.Controllers;

[ApiController]
[Route("api/auth/opaque")]
[Authorize(Policy = "ExternalOpaqueOnly")]
public sealed class OpaqueIdentityController : ControllerBase
{
    /// <summary>A diagnostic resource secured by verified OAuth token introspection.</summary>
    [HttpGet("me")]
    public IActionResult Me() => Ok(new
    {
        subject = User.FindFirstValue("sub"),
        issuer = User.FindFirstValue("iss"),
        authentication = OpaqueIntrospectionHandler.SchemeName
    });
}
