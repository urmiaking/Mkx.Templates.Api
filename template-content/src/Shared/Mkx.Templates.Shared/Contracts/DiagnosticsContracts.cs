namespace Mkx.Templates.Shared.Contracts;

/// <summary>Diagnostics never imply local Identity role authorization.</summary>
public sealed record ExternalIdentityResponse(string? Subject, string? ClientId, string? Issuer, string Authentication);
public sealed record OpaqueIdentityResponse(string? Subject, string? Issuer, string Authentication);
public sealed record IntegrationPingResponse(string Status, string? Client);
public sealed record HealthLiveResponse(string Status);
