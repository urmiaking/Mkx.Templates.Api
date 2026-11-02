using System.ComponentModel.DataAnnotations;

namespace Mkx.Templates.Shared.Contracts;

public sealed record ExternalLinkChallengeResponse(string Challenge, DateTimeOffset ExpiresAt);
public sealed record CompleteExternalLinkRequest([Required, StringLength(128, MinimumLength = 20)] string Challenge);
public sealed record ExternalLinkedAccountResponse(Guid Id, string UserName, string? DisplayName);