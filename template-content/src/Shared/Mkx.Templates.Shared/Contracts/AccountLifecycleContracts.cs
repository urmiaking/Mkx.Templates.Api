using System.ComponentModel.DataAnnotations;
namespace Mkx.Templates.Shared.Contracts;
public sealed record RegisterAccountRequest([Required,StringLength(60,MinimumLength=3)] string Username,[Required,EmailAddress,StringLength(256)] string Email,[Required,StringLength(256,MinimumLength=12)] string Password,[Required,StringLength(160)] string DisplayName);
public sealed record ConfirmAccountRequest([Required] string Username,[Required] string Token);
public sealed record AccountRecoveryRequest([Required] string Username,[Required,EmailAddress] string Email);
public sealed record ResetAccountPasswordRequest([Required] string Username,[Required] string Token,[Required,StringLength(256,MinimumLength=12)] string NewPassword);
