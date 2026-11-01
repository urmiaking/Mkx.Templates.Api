using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mkx.Templates.Domain.Identity;
namespace Mkx.Templates.Server.Controllers;
[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/users")]
public sealed class UsersController(UserManager<AppUser> users) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = users.Users.AsNoTracking();
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.UserName).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.UserName, x.DisplayName }).ToListAsync(ct);
        return Ok(new { items, total, page, pageSize });
    }
}
