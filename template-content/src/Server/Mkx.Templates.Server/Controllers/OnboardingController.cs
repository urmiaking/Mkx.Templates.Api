using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mkx.Templates.Application.Abstractions;
using Mkx.Templates.Shared.Contracts;
using Mkx.Templates.Server.Hosting;

namespace Mkx.Templates.Server.Controllers;

/// <summary>Optional username-based account onboarding. Never issues JWT before confirmation.</summary>
[ApiController]
[Route("api/onboarding")]
[AllowAnonymous]
[EnableRateLimiting("onboarding")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OnboardingController(
    IAccountLifecycleService lifecycle, AccountLifecycleFeatureState state) : ControllerBase
{
    private bool Enabled => state.Enabled;

    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Register(RegisterAccountRequest request, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        // Mask username/email collisions. No session is created before email confirmation.
        await lifecycle.RegisterAsync(request, ct);
        return Accepted();
    }

    [HttpPost("confirm")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Confirm(ConfirmAccountRequest request, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        return await lifecycle.ConfirmAsync(request, ct) ?
            NoContent() : BadRequest(new { message = "Invalid or expired confirmation code." });
    }

    [HttpPost("resend-confirmation")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Resend(AccountRecoveryRequest request, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        await lifecycle.RequestConfirmationAsync(request, ct);
        return Accepted();
    }

    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ForgotPassword(AccountRecoveryRequest request, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        await lifecycle.RequestPasswordResetAsync(request, ct);
        return Accepted();
    }

    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(ResetAccountPasswordRequest request, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        return await lifecycle.ResetPasswordAsync(request, ct) ?
            NoContent() : BadRequest(new { message = "Invalid or expired password reset code." });
    }
}
