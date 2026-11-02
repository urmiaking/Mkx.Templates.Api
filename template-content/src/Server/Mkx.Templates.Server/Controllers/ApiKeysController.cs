using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mkx.Templates.Application.Abstractions;
using Mkx.Templates.Shared.Contracts;

namespace Mkx.Templates.Server.Controllers;

/// <summary>Administrative credential lifecycle. Never returns key material after creation.</summary>
[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/api-keys")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ApiKeysController(IApiKeyService keys) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(CreatedApiKeyResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreateApiKeyRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var creatorId))
            return Unauthorized();

        var created = await keys.CreateAsync(creatorId, request, ct);
        if (created is null)
            return ValidationProblem("API key name, scopes or validity period is not allowed.");

        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ApiKeySummary>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await keys.ListAsync(ct));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
        => await keys.RevokeAsync(id, ct) ? NoContent() : NotFound();
}
