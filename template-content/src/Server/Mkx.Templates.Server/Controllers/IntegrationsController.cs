using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mkx.Templates.Shared.Contracts;

namespace Mkx.Templates.Server.Controllers;

[ApiController]
[Route("api/integrations")]
[EnableRateLimiting("auth")]
public sealed class IntegrationsController : ControllerBase
{
    /// <summary>Example endpoint. A scope must be granted explicitly to this credential.</summary>
    [HttpGet("ping")]
    [Authorize(Policy = "IntegrationRead")]
    [ProducesResponseType(typeof(IntegrationPingResponse), StatusCodes.Status200OK)]
    public IActionResult Ping() =>
        Ok(new IntegrationPingResponse("ok", User.FindFirst("client_name")?.Value));
}
