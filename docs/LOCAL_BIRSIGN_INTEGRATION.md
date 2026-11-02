# Local BirSign ↔ Mkx.Templates.Api end-to-end validation

**Target: local Windows SQL Server only.** BirSign STS Development configuration selects `localhost/BirSignDb`, not `IdentityServerAdmin` or production. Duende IdentityServer 7.4.3 runs at `https://localhost:44310` and exposes HTTPS discovery/JWKS and `/connect/token`.

## One-time registration

From the Mkx.Templates.Api repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/birsign-local-provision.ps1
```

The script creates **only if absent** an isolated machine client `mkx.api.local.smoke`, OAuth scope `mkx.api.read` and API resource `mkx.api`, using a SQL SERIALIZABLE transaction. It does not touch existing clients. Only `client_credentials` is enabled; no authorization-code redirect URI, user session, administrator role or browser access. The 48-byte generated secret is stored hashed as Duende-compatible SHA256/Base64 in SQL; its usable form is protected by Windows user DPAPI in `%LOCALAPPDATA%\Mkx.Templates.Api\BirSignLocalSmoke.dpapi` (never in Git). Client secret expires after 30 days. Rerunning the provisioner checks the same registration without inserting duplicates or replacing settings.

The targeted rows were absent before insertion, so the transaction provides complete rollback on failure. The unrelated approximately 38-GB development database was not backed up in full.

## Run both services locally

BirSign project directory:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:ASPNETCORE_URLS='https://localhost:44310'
dotnet run --project src/MapIdeaHub.IdentityServer4.STS.Identity/MapIdeaHub.IdentityServer4.STS.Identity.csproj --no-launch-profile
```

Mkx.Templates.Api directory (separate terminal):

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:ASPNETCORE_URLS='http://localhost:5287'
$env:ExternalOidc__Enabled='true'
$env:ExternalOidc__Authority='https://localhost:44310'
$env:ExternalOidc__Audience='mkx.api'
$env:ExternalOidc__RequiredScope='mkx.api.read'
$env:Jwt__SigningKey=[Guid]::NewGuid().ToString('N')+[Guid]::NewGuid().ToString('N')
dotnet run --project template-content/src/Server/Mkx.Templates.Server/Mkx.Templates.Server.csproj --no-launch-profile
```

The loopback HTTP API URL is for local development **only**; external traffic and production require TLS.

## Live security test

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/birsign-local-smoke.ps1
```

It requests a **real JWT** from local BirSign via `client_credentials`, checks issuer/audience/scope, calls `GET /api/auth/external/me` on the Mkx API and confirms success, then verifies that the same external token gets **401** from local-administrator-only `GET /api/users`. Use `-TokenOnly` to check the STS without starting Mkx API.

The scripts never print credentials or tokens, and do not access production. This demonstrates service-to-service OIDC resource-server integration. For human browser/mobile SSO, register a different client using Authorization Code + PKCE and a genuine redirect URI; do not reuse this smoke client. The local test does not claim testing production BirSign or its real-user login flow.
