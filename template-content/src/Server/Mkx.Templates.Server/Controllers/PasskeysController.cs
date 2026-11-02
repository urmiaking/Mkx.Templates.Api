using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mkx.Templates.Application.Abstractions;
using Mkx.Templates.Server.Hosting;
using Mkx.Templates.Shared.Contracts;

namespace Mkx.Templates.Server.Controllers;

/// <summary>Opt-in WebAuthn/passkeys for a JWT-only API; server holds every challenge.</summary>
[ApiController]
[Route("api/passkeys")]
[EnableRateLimiting("auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PasskeysController(IPasskeyService passkeys, PasskeyFeatureState feature) : ControllerBase
{
    private bool TryUserId(out Guid id) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out id);

    [HttpPost("register/options")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(PasskeyCeremonyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegisterOptions(PasskeyEnrollmentRequest request, CancellationToken ct)
    {
        if (!feature.Enabled) return NotFound();
        if (!TryUserId(out var id)) return Unauthorized();
        var options = await passkeys.BeginRegistrationAsync(id, request, HttpContext, ct);
        return options is null ? BadRequest(new { message = "Passkey enrollment is unavailable." }) : Ok(options);
    }

    [HttpPost("register/complete")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterComplete(PasskeyCompletionRequest request, CancellationToken ct)
    {
        if (!feature.Enabled) return NotFound();
        if (!TryUserId(out var id)) return Unauthorized();
        return await passkeys.FinishRegistrationAsync(id, request, HttpContext, ct) ?
            NoContent() : BadRequest(new { message = "Invalid or expired WebAuthn registration." });
    }

    [HttpPost("login/options")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PasskeyCeremonyResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> LoginOptions(CancellationToken ct)
    {
        if (!feature.Enabled) return NotFound();
        return Ok(await passkeys.BeginLoginAsync(HttpContext, ct));
    }

    [HttpPost("login/complete")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LoginComplete(PasskeyCompletionRequest request, CancellationToken ct)
    {
        if (!feature.Enabled) return NotFound();
        return await passkeys.FinishLoginAsync(request, HttpContext, ct) is { } token ?
            Ok(token) : Unauthorized();
    }

    [HttpGet]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(IReadOnlyList<PasskeySummary>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (!feature.Enabled) return NotFound();
        if (!TryUserId(out var id)) return Unauthorized();
        return await passkeys.ListAsync(id, ct) is { } found ? Ok(found) : NotFound();
    }

    [HttpPost("remove")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Remove(PasskeyRemoveRequest request, CancellationToken ct)
    {
        if (!feature.Enabled) return NotFound();
        if (!TryUserId(out var id)) return Unauthorized();
        return await passkeys.RemoveAsync(id, request, ct) ?
            NoContent() : BadRequest(new { message = "Passkey removal requires valid credentials." });
    }
}
