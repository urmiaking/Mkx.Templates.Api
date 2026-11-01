using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Mkx.Templates.Domain.Identity;
using Mkx.Templates.Server.Auth;
using Xunit;

namespace Mkx.Templates.Tests;

public sealed class JwtSecurityTests
{
    private static readonly JwtOptions Settings = new()
    {
        Issuer = "test-issuer",
        Audience = "test-client",
        SigningKey = new string('Z', 64),
        AccessTokenMinutes = 15
    };

    [Fact]
    public void IssuedAccessTokenContainsExpectedClaimsAndHasValidSignature()
    {
        var user = new AppUser { Id = Guid.NewGuid(), UserName = "alice" };
        var (jwt, expires) = new JwtTokenIssuer(Options.Create(Settings), TimeProvider.System)
            .Issue(user, ["Administrator"]);
        var principal = Validate(jwt);

        Assert.Equal(user.Id.ToString(), principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.True(principal.IsInRole("Administrator"));
        Assert.Equal("access", principal.FindFirst("token_use")?.Value);
        Assert.True(expires > DateTimeOffset.UtcNow);
    }

    [Fact]
    public void TamperedAccessTokenDoesNotValidate()
    {
        var user = new AppUser { Id = Guid.NewGuid(), UserName = "alice" };
        var (jwt, _) = new JwtTokenIssuer(Options.Create(Settings), TimeProvider.System)
            .Issue(user, Array.Empty<string>());
        var segments = jwt.Split('.');
        segments[2] = segments[2][0] == 'A' ? "B" + segments[2][1..] : "A" + segments[2][1..];
        Assert.ThrowsAny<SecurityTokenException>(() => Validate(string.Join(".", segments)));
    }

    private static ClaimsPrincipal Validate(string jwt)
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        return handler.ValidateToken(jwt, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Settings.SigningKey)),
            ValidateIssuer = true, ValidIssuer = Settings.Issuer,
            ValidateAudience = true, ValidAudience = Settings.Audience,
            ValidateLifetime = true, ClockSkew = TimeSpan.Zero,
            NameClaimType = ClaimTypes.Name, RoleClaimType = ClaimTypes.Role
        }, out _);
    }
}

