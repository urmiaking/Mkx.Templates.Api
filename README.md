# Mkx.Templates.Api

**Latest release:** [v0.8.1](https://github.com/urmiaking/Mkx.Templates.Api/releases/tag/v0.8.1) — NuGet template package attached; [CI passing](https://github.com/urmiaking/Mkx.Templates.Api/actions/runs/41488496536).

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

### Service credentials and federated API callers

- Administrators using **local JWT** can create expiring scoped API keys at POST /api/api-keys, list metadata at GET /api/api-keys, and revoke a key at DELETE /api/api-keys/{id}.
- A service calls GET /api/integrations/ping with X-API-Key and integration:read scope. Never use API keys inside mobile clients.
- Optional external OIDC JWT access-token validation is configured via ExternalOidc:Enabled, Authority (HTTPS) and Audience (resource API). Native clients use the IdP's authorization code + PKCE rather than the local login endpoint.
- Apply AddApiClientKeys migration to existing databases. Configuration and exact security boundaries: [API Keys](template-content/docs/ai/API_KEYS.md) and [External OIDC](template-content/docs/ai/EXTERNAL_OIDC.md).

This is not a self-hosted OAuth authorization server. OpenIddict/opaque introspection and live BirSign integration are explicitly deferred.
## API developer portal

Real local BirSign STS test and safe, scoped SQL registration: [runbook](docs/LOCAL_BIRSIGN_INTEGRATION.md). Production and existing clients are untouched.

## Native user PKCE and external account linking

The LOCAL BirSign public PKCE client is registered by scripts/birsign-local-pkce.sql and tested with scripts/birsign-local-pkce-smoke.ps1. Separate endpoints under /api/auth/external-links associate verified user issuer+subject with a confirmed nonadmin local account using a one-time, hashed challenge. See template-content/docs/ai/ACCOUNT_LINKING.md. No automatic user provisioning or full interactive login is claimed.

## Optional account registration and recovery
Phase 7.1 introduces username registration with email confirmation and token-based password reset, disabled by default. Configure persistent Data Protection keys and authenticated STARTTLS SMTP delivery before enabling it. The setup and limitations are documented in template-content/docs/ai/ACCOUNT_LIFECYCLE.md. MFA and passkeys are not enabled.

## TOTP authenticator MFA

Phase 7.2 supports standard authenticator apps, password plus six-digit TOTP or one-time recovery code, and secure disablement. A SQL-backed replay fence prevents reusing OTPs, and MFA changes revoke refresh tokens. Read template-content/docs/ai/AUTHENTICATOR_MFA.md. SMS OTP and WebAuthn/passkeys remain future work.

## Optional WebAuthn / Passkeys (Phase 7.3)

The .NET 10 API template now includes opt-in native Identity passkeys: discoverable account login, server-backed one-time challenges, registration after local account verification, list/remove and normal local JWT/refresh issuance after a verified WebAuthn assertion. Explicit Passkeys RelyingPartyId and Origin are mandatory; MFA accounts still need a second factor. See template-content/docs/ai/PASSKEYS.md. A full signed credential/device ceremony is not yet verified.

## TypeScript and Dart SDKs (Phase 8)

The generated solution includes local-path TypeScript (clients/typescript) and Dart/Flutter (clients/dart) SDKs for all API operations, plus minimal React Native/Expo and Flutter secure-session integration examples. The canonical openapi/v1.json is exported from the actual Development API; tools/generate_sdks.py updates the models and clients. CI checks API/schema/SDK drift, token refresh and scheme isolation. See template-content/docs/ai/SDK_GENERATION.md for setup and limitations. The NuGet template is released on GitHub; it is not automatically published to NuGet.org and the SDKs are not registered on npm or pub.dev.
