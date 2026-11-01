# Mkx.Templates.Api — Roadmap

Last updated: 2026-11-02. Phase completion requires implementation, actual verification, and an entry in [PHASE_LOG.md](PHASE_LOG.md).

| Phase | Scope | Status | Evidence |
|---|---|---|---|
| 0 | Inspect Blazor backend architecture and decide module boundaries | Completed | [Architecture](ARCHITECTURE.md), [ADR 0001](decisions/0001-backend-architecture.md) |
| 1 | Standalone API-only solution, NuGet package and dotnet-new template | Completed | Six .NET 10 projects, package v0.1.0, generated project builds |
| 2 | Identity username/password, JWT Bearer, refresh rotation, revocation and admin policy | Completed (baseline) | 6 unit/API tests + real database-backed auth smoke |
| 3 | SQL Server migrations, rate limiting, CORS, errors, Swagger, Docker, health | Completed (baseline) | InitialIdentity migration, database ready health |
| 4 | Source/generator/build/security verification | Completed | 0-warning source/generated builds; 6/6 tests for each; full SQL smoke and cleanup |
| 5 | GitHub repository, CI, push, release | In progress | GitHub verification pending |
| 6 | Optional external OIDC (BirSign/Keycloak), OpenIddict server, API keys | Planned | Use OAuth authorization code + PKCE for native clients |
| 7 | Account registration/recovery, OTP/MFA, WebAuthn/passkeys | Planned | Provider-specific configuration |
| 8 | Typed TypeScript and Dart SDKs, mobile samples, public NuGet publication | Planned | Stabilize API contracts first |

## Definition of done

1. Code implemented in the correct backend layer, with documentation reflecting any architecture change.
2. `dotnet build Mkx.Templates.slnx` from `template-content` passes.
3. `dotnet test --project Tests/Mkx.Templates.Tests/Mkx.Templates.Tests.csproj` passes from `template-content`. (.NET 10 Microsoft.Testing.Platform requires using the application's `global.json`; invoking `dotnet test` on the solution directly discovered zero tests.)
4. NuGet package installs; a new `dotnet new mkx-api` project builds and tests.
5. For persistence/security work, optional local `scripts/smoke-sql.ps1` passes against a disposable SQL Server database.
6. Update [Phase Log](PHASE_LOG.md) with verification results and limitations.

**Version 0.1.0 is an API authentication baseline, not an OAuth/OIDC provider.** External federation, self-service registration, OTP, passkeys, API keys, and client SDK generation are future phases. JWT access tokens remain valid until expiration after refresh-token revocation.
