using System.ComponentModel.DataAnnotations;
namespace Mkx.Templates.Shared.Contracts;
public sealed record LoginRequest([Required] string Username, [Required] string Password);
public sealed record RefreshRequest([Required] string RefreshToken);
public sealed record RevokeRequest([Required] string RefreshToken);
public sealed record AccountResponse(Guid Id, string Username, string DisplayName, IReadOnlyList<string> Roles);
public sealed record TokenResponse(string AccessToken, string RefreshToken, DateTimeOffset AccessTokenExpiresAt, DateTimeOffset RefreshTokenExpiresAt, string TokenType = "Bearer");
