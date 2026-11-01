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

## 2026-11-02 · Phase 5 — In progress: GitHub delivery

Create public repository alongside the existing public Mkx template repos, push the initial source, verify GitHub Actions and attach initial package/release.

## Deferred phases

6: OIDC/OpenIddict/API Keys; 7: recovery, MFA/OTP, passkeys and registration; 8: TypeScript/Dart SDKs and examples. These are documented plans and **not** delivered features in 0.1.0.
