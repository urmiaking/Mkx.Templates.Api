# Mkx.Templates.Api — Phase execution log

## 2026-11-02 · Phase 0 — Completed: Inspect Blazor architecture

Inspected `D:\source\Mkx.Templates.Blazor` and read its AGENTS, ARCHITECTURE, AUTHENTICATION, DATABASE, SERVICE_ARCHITECTURE and GUARDRAILS guidance. Adopted its pragmatic Domain / Infrastructure / Shared / Application / Server layers, retaining Application-to-Infrastructure dependency and excluding SSR/WASM/Client/cookie-specific adapters. Documented the tradeoff in ADR 0001.

## 2026-11-02 · Phase 1 — Completed: Scaffold and packaging

Created `D:\source\Mkx.Templates.Api` alongside other templates and registered with MateMCP. Added six .NET 10 projects (.slnx), template metadata (shortName `mkx-api`), central package management, and `Mkx.Templates.Api.csproj` NuGet template package (v0.1.0). Installed actual .nupkg, generated `Portable.Api`, built it with zero warnings and errors, and ran the six package-generated tests successfully.

## 2026-11-02 · Phase 2 — Completed: Identity/JWT baseline

Implemented username-based Identity and confirmed accounts, 12-character passwords and lockout, JWT Bearer access tokens, random hashed 30-day refresh tokens, transactional rotation, replay-family invalidation, per-session logout and all-session logout, role-protected admin listing, and opt-in administrator bootstrap with no default credentials. Token signing key must be supplied securely.

The first real SQL Server smoke test found an EF Core error: `SqlServerRetryingExecutionStrategy` rejected the explicit user transaction during refresh. Fixed by wrapping the complete transaction in `CreateExecutionStrategy().ExecuteAsync`. Subsequent end-to-end test passed all authentication scenarios.

## 2026-11-02 · Phase 3 — Completed: Hosting, persistence and operations

Created the initial `InitialIdentity` EF migration. Added MVC ProblemDetails, development Swagger, explicit-origin CORS, auth endpoint IP rate limiting, HSTS/HTTPS production defaults, SQL connectivity health checks, Dockerfile and operational docs. Migration applied successfully to temporary local SQL Server database; ready health returned healthy. The test database was deleted.

## 2026-11-02 · Phase 4 — Completed: Verification

- Source: `dotnet build Mkx.Templates.slnx` from `template-content`: 0 warnings, 0 errors.
- Source: `dotnet test --project Tests/Mkx.Templates.Tests/Mkx.Templates.Tests.csproj`: **6/6 passed**.
- NuGet packaged template: installed, generated project `Portable.Api`, build 0 warnings/0 errors and **6/6 generated tests passed**.
- SQL Server: `scripts/smoke-sql.ps1` returned LOGIN, ME, ADMIN_POLICY, REFRESH, LOGOUT, REVOKE and READY all PASS; random temporary DB dropped with exit code 0.
- .NET 10 Microsoft.Testing.Platform: `dotnet test` with a solution target discovered zero tests; direct `--project` invocation from the directory containing `global.json` is the verified path and is used in CI.

## 2026-11-02 · Phase 5 — Completed: GitHub delivery

Created public repository https://github.com/urmiaking/Mkx.Templates.Api and pushed initial commit 6a2451e to main. GitHub Actions run https://github.com/urmiaking/Mkx.Templates.Api/actions/runs/41358930031 completed with success for build, test, NuGet pack, generated project build and generated project tests. Published https://github.com/urmiaking/Mkx.Templates.Api/releases/tag/v0.1.0 (non-draft release) with attached Mkx.Templates.Api.0.1.0.nupkg. Created issues #1/#2/#3 for planned phases 6/7/8. No NuGet.org publication was attempted; it is planned in Phase 8.

## Deferred phases

6: OIDC/OpenIddict/API Keys; 7: recovery, MFA/OTP, passkeys and registration; 8: TypeScript/Dart SDKs and examples. These are documented plans and **not** delivered features in 0.1.0.

## 2026-11-02 · Phase 5.1 — Stabilization of API baseline (local validation completed)

- Recovered a clean main branch at 4db4c1a; prior GitHub CI for initial source/docs was green.
- Hardened refresh-token handling: a rotated token replay invalidates the complete family, including a losing competing refresh request. Kept EF SQL retry execution strategy and atomic update transaction.
- Added fallback authorization: new endpoints require authentication unless marked AllowAnonymous. Accounts with TwoFactorEnabled are denied local login/refresh until MFA is actually supported. Login/refresh and live/ready health endpoints are explicitly public.
- Auth IP rate limiting is configurable (RateLimit:AuthRequestsPerMinute, default 10; local integration smoke overrides to 60 only in that process).
- Tests expanded from six to eight, including authorization fallback and anonymous malformed login validation.
- Real SQL smoke passed LOGIN, ME, ADMIN_POLICY, REFRESH, REPLAY, REPLAY_FAMILY_REVOCATION, RELOGIN, LOGOUT, REVOKE, LOGOUT_ALL and READY; ephemeral database dropped successfully.
- NuGet template package version raised to 0.1.1 and successfully installed from the .nupkg. Generated Stable.Smoke solution built with 0 warnings/errors; all 8 generated tests passed. GitHub CI for commit 48f9ce0 (run 41360215260) passed the application build, tests, NuGet pack, generated solution build and tests. Updated checkout/setup-dotnet actions to the latest stable GitHub release majors (v7/v6); verify these in the next CI run before the release.

Scope remains API-only Identity + JWT. OIDC, passkeys, SMS/OTP, account recovery and generated mobile SDKs are still planned, not delivered.