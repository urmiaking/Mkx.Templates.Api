using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Mkx.Templates.Application.Abstractions;
using Mkx.Templates.Shared.Contracts;
using Mkx.Templates.Shared.Routes;
namespace Mkx.Templates.Server.Controllers;
[ApiController]
[Route(ApiRoutes.Auth)]
[EnableRateLimiting("auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthController(IAuthService auth) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost(ApiRoutes.Login)]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
        => await auth.LoginAsync(request, ct) is { } tokens ? Ok(tokens) : Unauthorized();

    [AllowAnonymous]
    [HttpPost(ApiRoutes.Refresh)]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(RefreshRequest request, CancellationToken ct)
        => await auth.RefreshAsync(request, ct) is { } tokens ? Ok(tokens) : Unauthorized();

    [Authorize]
    [HttpPost(ApiRoutes.Logout)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(RevokeRequest request, CancellationToken ct)
    {
        if (!TryUserId(out var id)) return Unauthorized();
        await auth.RevokeAsync(id, request, ct);
        return NoContent();
    }

    [Authorize]
    [HttpPost("logout-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LogoutAll(CancellationToken ct)
    {
        if (!TryUserId(out var id)) return Unauthorized();
        await auth.RevokeAllAsync(id, ct);
        return NoContent();
    }

    [Authorize]
    [HttpGet(ApiRoutes.Me)]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        if (!TryUserId(out var id)) return Unauthorized();
        var me = await auth.GetMeAsync(id, ct);
        return me is null ? NotFound() : Ok(me);
    }

    private bool TryUserId(out Guid id) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out id);
}

