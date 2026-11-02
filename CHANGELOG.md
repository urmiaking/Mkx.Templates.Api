# Changelog

## 0.7.0 - Opt-in WebAuthn/Passkeys for API-only apps (Phase 7.3)

- .NET 10 Identity passkey handler verifies WebAuthn attestation and signed assertions; verified passkeys persist in the Identity v3 AspNetUserPasskeys table.
- Discoverable usernameless passkey sign-in issues local JWT and rotating refresh tokens after verified assertion; local MFA still requires an additional TOTP or recovery code.
- Local-JWT and password-protected registration/credential management, bound to user and security stamp.
- SQL-backed, 3-minute, atomically one-use WebAuthn challenges with replay/owner/purpose/expiration checks and lazy pruning; explicit RP/Origin configuration is required before opt-in.
- Swagger contracts, API and Identity tests, real SQL Server options and invalid-attestation replay smoke. Real signed device ceremony and mobile integration require further verification.

## 0.6.0 - TOTP authenticator MFA and recovery (Phase 7.2)

- Authenticator setup, verification, disablement and eight single-use recovery codes with password reauthentication.
- MFA-protected username/password login requires an additional verified TOTP or recovery code; existing refresh semantics preserved only for MFA-verified sessions.
- Persisted TOTP replay fence with unique SQL key and expiration; MFA state changes rotate security stamps and revoke refresh sessions.
- Additional Swagger operations, typed contracts, Identity integration tests and SQL migration. SMS/email OTP and passkeys are not implemented.

## 0.5.1 - Bootstrap administrator compatibility

- When onboarding and administrator bootstrap are both enabled, require a configured administrator email for Identity unique-email validation; do not weaken validation or create an unusable administrator.
- Add regression test for missing and valid BootstrapAdmin Email. Existing installations with onboarding disabled retain the previous bootstrap behavior.

## 0.5.0 - Optional account onboarding and recovery (Phase 7.1)

- Added anonymous, IP-throttled, opt-in username registration, email confirmation and password recovery APIs.
- Requires authenticated STARTTLS SMTP and a persistent Data Protection key ring before activation.
- No unconfirmed logins or local JWT issuance; generic recovery responses, expiring confirmation/reset codes, security-stamp invalidation and MFA/admin fail-closed behavior.
- Added OpenAPI contracts and Identity integration tests. MFA/OTP/passkeys and live SMTP delivery remain unverified future work.

## 0.4.0 - Public native PKCE registration and external identity account linking

- Isolated LOCAL BirSign authorization-code client requiring S256 PKCE and no client secret; real discovery and authorize redirect verified.
- Two-party explicit local/external identity linking via a five-minute, one-time hashed challenge. Unique issuer/subject indexes, unlink and linked-profile lookup; no automatic role mapping or local JWT exchange.
- New EF migration, OpenAPI operations, SQLite security tests and local SQL migration smoke. Interactive human login/code exchange remains deferred.

## 0.3.1 — Verified local BirSign OIDC integration (2026-11-02)

- Real local Duende STS-issued JWT accepted by the API resource endpoint, denied access to local administrator endpoints.
- Exact configurable external scope requirement, machine-token client_id support, five scope policy tests.
- Local-only idempotent client/scope/resource SQL provisioning and token smoke, with DPAPI-protected credential; no production changes.

## 0.3.0 — Introspection and API developer portal (2026-11-02)

- Optional RFC 7662 HTTPS opaque token introspection with strict security checks and upstream mock testing.
- Dark responsive Swagger UI with stable API contracts, security schemes, response DTOs and preserved agent instructions.
- Existing Identity JWT, external JWT and API keys remain independent. Live IdP testing and full OpenIddict server are deferred.

## 0.2.0 — External identity and service key foundation (2026-11-02)

- Optional standards-based OIDC external JWT access-token validation via HTTPS Authority/JWKS and API Audience, isolated from local Identity/JWT/admin.
- Service-to-service hashed API keys with explicit scopes, expiry, admin-managed issuance and revocation.
- New EF Core migration AddApiClientKeys, diagnostic integration endpoints, documentation, and 17 automated tests.
- Live SQL Server smoke verified key issuance/use/scope denial/revocation without revealing raw key material.
- Remaining: self-hosted OpenIddict server, opaque token introspection, external user linking and live IdP compatibility verification.

## 0.1.1 — Stable baseline patch (2026-11-02)

- Fail closed for TwoFactorEnabled accounts until MFA is implemented.
- Improved refresh-token replay handling across session families, including competing refresh attempts.
- Added default-deny API authorization fallback and explicit anonymous authentication/health endpoints.
- Made auth request rate limiting configurable (default 10 per IP/minute).
- Added regression coverage for default authorization and malformed sign-in requests.
- Expanded disposable SQL Server integration smoke with replay-family and logout-all checks.
- Updated architecture, operations and phase-by-phase delivery documentation.

## 0.1.0 — Initial API foundation (2026-11-02)

- Backend-only ASP.NET Core 10 solution with Identity, JWT, hashed refresh tokens, EF SQL Server and migrations.
- GitHub CI, NuGet template packaging, API tests and local SQL smoke.