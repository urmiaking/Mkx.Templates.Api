using System.ComponentModel.DataAnnotations;
namespace Mkx.Templates.Server.Auth;
public sealed class JwtOptions
{
    public const string Section = "Jwt";
    [Required] public string Issuer { get; set; } = "Mkx.Templates.Api";
    [Required] public string Audience { get; set; } = "Mkx.Templates.Clients";
    [Required, MinLength(32)] public string SigningKey { get; set; } = "";
    [Range(1, 60)] public int AccessTokenMinutes { get; set; } = 15;
}
