using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mkx.Templates.Server.Auth;
using Mkx.Templates.Shared.Contracts;

namespace Mkx.Templates.Server.Controllers;

[ApiController]
[Route("api/auth/opaque")]
[Authorize(Policy = "ExternalOpaqueOnly")]
public sealed class OpaqueIdentityController : ControllerBase
{
    /// <summary>A diagnostic resource secured by verified OAuth token introspection.</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(OpaqueIdentityResponse), StatusCodes.Status200OK)]
    public IActionResult Me() => Ok(new OpaqueIdentityResponse(User.FindFirstValue("sub"), User.FindFirstValue("iss"), OpaqueIntrospectionHandler.SchemeName));
}
