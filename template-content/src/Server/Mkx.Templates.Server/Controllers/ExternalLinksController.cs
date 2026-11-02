using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mkx.Templates.Application.Abstractions;
using Mkx.Templates.Shared.Contracts;

namespace Mkx.Templates.Server.Controllers;

[ApiController]
[Route("api/auth/external-links")]
[EnableRateLimiting("auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ExternalLinksController(
    IExternalAccountLinkService links, IConfiguration configuration) : ControllerBase
{
    [HttpPost("challenge")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(ExternalLinkChallengeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Challenge(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            return Forbid();
        var proof = await links.StartAsync(id, ct);
        return proof is null ? Forbid() : Ok(proof);
    }

    [HttpPost("complete")]
    [Authorize(Policy = "ExternalOidcOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Complete(CompleteExternalLinkRequest request, CancellationToken ct)
    {
        var issuer = configuration["ExternalOidc:Authority"]?.TrimEnd('/');
        var subject = User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject))
            return Forbid();
        return await links.CompleteAsync(issuer, subject, request.Challenge, ct) ?
            NoContent() : Conflict(new { message = "Challenge expired, reused or identity already linked." });
    }

    [HttpGet("me")]
    [Authorize(Policy = "ExternalOidcOnly")]
    [ProducesResponseType(typeof(ExternalLinkedAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var issuer = configuration["ExternalOidc:Authority"]?.TrimEnd('/');
        var subject = User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject))
            return Forbid();
        var account = await links.ResolveAsync(issuer, subject, ct);
        return account is null ? NotFound() : Ok(account);
    }

    [HttpDelete]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unlink(CancellationToken ct)
    {
        var issuer = configuration["ExternalOidc:Authority"]?.TrimEnd('/');
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ||
            string.IsNullOrWhiteSpace(issuer))
            return Forbid();
        return await links.UnlinkAsync(id, issuer, ct) ? NoContent() : NotFound();
    }
}