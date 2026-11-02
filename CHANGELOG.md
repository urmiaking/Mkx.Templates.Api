# Changelog

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