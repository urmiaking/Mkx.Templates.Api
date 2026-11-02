using System.ComponentModel.DataAnnotations;

namespace Mkx.Templates.Shared.Contracts;

/// <summary>OptionsJson contains WebAuthn options; parse as JSON before calling navigator.credentials.</summary>
public sealed record PasskeyCeremonyResponse(Guid CeremonyId, string OptionsJson);
public sealed record PasskeyEnrollmentRequest([Required] string Password,
    [RegularExpression(@"^\d{6}$")] string? AuthenticatorCode = null,
    [StringLength(32, MinimumLength = 8)] string? RecoveryCode = null);
public sealed record PasskeyCompletionRequest(
    Guid CeremonyId,
    [Required, StringLength(20000, MinimumLength = 2)] string CredentialJson,
    [RegularExpression(@"^\d{6}$")] string? AuthenticatorCode = null,
    [StringLength(32, MinimumLength = 8)] string? RecoveryCode = null);
public sealed record PasskeySummary(string CredentialId, string? Name);
public sealed record PasskeyRemoveRequest([Required] string CredentialId, [Required] string Password,
    [RegularExpression(@"^\d{6}$")] string? AuthenticatorCode = null,
    [StringLength(32, MinimumLength = 8)] string? RecoveryCode = null);
