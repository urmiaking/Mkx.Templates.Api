using System.ComponentModel.DataAnnotations;

namespace Mkx.Templates.Shared.Contracts;

/// <summary>Authenticator setup secret is shown once during explicit enrollment only; never log or cache.</summary>
public sealed record AuthenticatorSetupRequest([Required] string Password);
public sealed record AuthenticatorSetupResponse(string SharedKey, string AuthenticatorUri);
public sealed record AuthenticatorEnableRequest([Required, RegularExpression(@"^\d{6}$")] string AuthenticatorCode);
public sealed record AuthenticatorEnableResponse(IReadOnlyList<string> RecoveryCodes);
public sealed record AuthenticatorDisableRequest(
    [Required] string Password,
    [RegularExpression(@"^\d{6}$")] string? AuthenticatorCode = null,
    [StringLength(32, MinimumLength = 8)] string? RecoveryCode = null);
public sealed record AuthenticatorStatusResponse(bool Enabled, bool SetupStarted, int RecoveryCodesLeft);
