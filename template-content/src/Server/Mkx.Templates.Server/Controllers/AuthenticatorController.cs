using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mkx.Templates.Application.Abstractions;
using Mkx.Templates.Shared.Contracts;

namespace Mkx.Templates.Server.Controllers;

/// <summary>Authenticator TOTP enrollment and lifecycle. Requires an existing local JWT.</summary>
[ApiController]
[Route("api/auth/authenticator")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[EnableRateLimiting("auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthenticatorController(IAuthenticatorService service) : ControllerBase
{
    private bool TryUserId(out Guid id) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out id);

    [HttpGet("status")]
    [ProducesResponseType(typeof(AuthenticatorStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        if (!TryUserId(out var id)) return Unauthorized();
        return await service.StatusAsync(id, ct) is { } value ? Ok(value) : NotFound();
    }

    [HttpPost("setup")]
    [ProducesResponseType(typeof(AuthenticatorSetupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Setup(AuthenticatorSetupRequest request, CancellationToken ct)
    {
        if (!TryUserId(out var id)) return Unauthorized();
        return await service.BeginSetupAsync(id, request.Password, ct) is { } result ?
            Ok(result) : BadRequest(new { message = "Authenticator setup is not permitted." });
    }

    [HttpPost("enable")]
    [ProducesResponseType(typeof(AuthenticatorEnableResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Enable(AuthenticatorEnableRequest request, CancellationToken ct)
    {
        if (!TryUserId(out var id)) return Unauthorized();
        return await service.EnableAsync(id, request.AuthenticatorCode, ct) is { } result ?
            Ok(result) : BadRequest(new { message = "Invalid authenticator code or unavailable enrollment." });
    }

    [HttpPost("disable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Disable(AuthenticatorDisableRequest request, CancellationToken ct)
    {
        if (!TryUserId(out var id)) return Unauthorized();
        return await service.DisableAsync(id, request, ct) ?
            NoContent() : BadRequest(new { message = "Authenticator verification failed." });
    }
}
