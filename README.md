# Mkx.Templates.Api

**Stable release:** [v0.1.1](https://github.com/urmiaking/Mkx.Templates.Api/releases/tag/v0.1.1) — NuGet template package attached; [CI passing](https://github.com/urmiaking/Mkx.Templates.Api/actions/runs/41360362576).

A backend-only ASP.NET Core 10 solution template, designed around the **same pragmatic Domain / Infrastructure / Shared / Application / Server layers as Mkx.Templates.Blazor**—without a frontend, Blazor, SSR or WASM.

The v0.1.0 baseline includes ASP.NET Core Identity (username/password and confirmed accounts), JWT Bearer, hashed rotating refresh tokens and revocation, role authorization, SQL Server/EF Core, Swagger, scoped CORS, rate limiting, health checks, Docker and six automated security/API tests.

## Create a project

```powershell
dotnet new install ./template-content
dotnet new mkx-api -n MyProduct -o D:\source\MyProduct
cd D:\source\MyProduct
dotnet build MyProduct.slnx
```

Before launch, set an independent random signing key through **user-secrets or environment**, not source control:

```powershell
dotnet user-secrets init --project src/Server/MyProduct.Server
dotnet user-secrets set "Jwt:SigningKey" "<strong-random-secret>" --project src/Server/MyProduct.Server
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<local-SQL-Server-connection>" --project src/Server/MyProduct.Server
```

Apply the included initial EF migration to a dedicated database. Optionally provision a first administrator through secure `BootstrapAdmin` configuration, then disable bootstrap; see [Operations](template-content/docs/ai/OPERATIONS.md).

```powershell
dotnet ef database update --project src/Core/MyProduct.Infrastructure --startup-project src/Server/MyProduct.Server
dotnet test --project Tests/MyProduct.Tests/MyProduct.Tests.csproj
dotnet run --project src/Server/MyProduct.Server
```

Development Swagger: `/swagger`. API includes `POST /api/auth/login`, `/refresh`, `/logout`, `/logout-all`, `GET /api/auth/me`, admin-only `GET /api/users`, and `/health/live` and `/health/ready`.

See [Roadmap](docs/ROADMAP.md), [Phase log](docs/PHASE_LOG.md), [Architecture](docs/ARCHITECTURE.md), and optional disposable SQL Server smoke script `scripts/smoke-sql.ps1`.

**Planned, not included yet:** external OIDC/OpenIddict, API-key integration, account self-registration/recovery, OTP/MFA/passkeys and generated TypeScript/Dart SDKs. Native public OAuth clients should use authorization code + PKCE after the OIDC phase.

## Phase 6 progress
Optional external JWT resource-server support is being implemented separately from local JWT auth. See [External OIDC guide](template-content/docs/ai/EXTERNAL_OIDC.md) and [Roadmap](docs/ROADMAP.md). OAuth token issuance and OpenIddict are not part of this stage.
