# Changelog

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