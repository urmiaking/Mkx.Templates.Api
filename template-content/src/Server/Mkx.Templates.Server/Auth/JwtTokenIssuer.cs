using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Mkx.Templates.Application.Abstractions;
using Mkx.Templates.Domain.Identity;
namespace Mkx.Templates.Server.Auth;
public sealed class JwtTokenIssuer(IOptions<JwtOptions> options, TimeProvider clock) : IAccessTokenIssuer
{
    public (string Token, DateTimeOffset ExpiresAt) Issue(AppUser user, IReadOnlyList<string> roles)
    {
        var opts = options.Value;
        var now = clock.GetUtcNow();
        var expiration = now.AddMinutes(opts.AccessTokenMinutes);
        var claims = new List<Claim> {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? ""),
            new("token_use", "access")
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opts.SigningKey));
        var token = new JwtSecurityToken(
            issuer: opts.Issuer, audience: opts.Audience,
            claims: claims, notBefore: now.UtcDateTime, expires: expiration.UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token), expiration);
    }
}
