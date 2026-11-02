using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Mkx.Templates.Server.Controllers;

[ApiController]
[Route("api/integrations")]
[EnableRateLimiting("auth")]
public sealed class IntegrationsController : ControllerBase
{
    /// <summary>Example endpoint. A scope must be granted explicitly to this credential.</summary>
    [HttpGet("ping")]
    [Authorize(Policy = "IntegrationRead")]
    public IActionResult Ping() =>
        Ok(new { status = "ok", client = User.FindFirst("client_name")?.Value });
}
