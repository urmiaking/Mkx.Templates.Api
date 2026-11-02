# Architecture

This API-only template follows Mkx.Templates.Blazor backend's pragmatic layers: Domain -> Infrastructure -> Application -> Server (dependency direction), and Shared for DTOs and route constants. No client UI.

- Domain: AppUser, RefreshSession; no EF configuration.
- Infrastructure: AppDbContext, Identity stores, SQL Server, EF mapping and optional specifications.
- Application: IAuthService, Identity user management, credential checking and session lifecycle.
- Server: HTTP controllers, JWT issuer/verifier, bootstrap admin, health, middleware, hosting.
- Tests: API pipeline and authorization smoke checks.

Controllers are adapters; use cases live in Application; HTTP contracts stay in Shared. No CQRS/MediatR. JWT signing key must come from a secret source. OAuth code + PKCE is deferred to a future standards-based OIDC module.

## Stable security baseline (v0.1.1)

- All endpoints require an authenticated caller unless explicitly AllowAnonymous; the login, refresh, live and ready health endpoints are public.
- Access tokens are JWT Bearer with 15-minute lifetimes. Refresh tokens are opaque, stored as SHA-256 hashes, atomically rotated in an EF SQL transaction, and related by token family. A repeated rotated token invalidates its family, including a losing concurrent rotation request.
- Auth endpoints are per-IP rate limited and return no-store responses. Trusted reverse-proxy configuration must be explicit in each deployment.
- Do not rely on UI authorization hiding; the server enforces role checks. CORS is allowlist-only for browsers.
## Optional authentication adapters (Phase 6)
The Server layer owns named ExternalOidc JWT Bearer and ApiKey authentication schemes. Application owns IApiKeyService and the key lifecycle; Infrastructure stores only hashed ApiClientKeys; Shared defines scope constants and administration contracts. AdminOnly explicitly uses the local JWT scheme. Resource endpoints must opt into the external or API-key policies; schemes never silently import external users or grant local Administrator. See EXTERNAL_OIDC.md and API_KEYS.md.

## API contracts and introspection

## External identity mapping

Domain owns ExternalIdentityLink and ExternalLinkChallenge; Infrastructure owns unique indexes and one-time proof persistence; Application ExternalAccountLinkService uses separate verified local and external proofs; Server ExternalLinksController enforces two independent schemes. Refer to ACCOUNT_LINKING.md for restrictions and lifecycle.

## Account lifecycle
Application AccountLifecycleService uses Identity user/token managers and IAccountMessageSender; Server OnboardingController exposes anonymous, throttled operations, while SmtpAccountMessageSender handles opt-in STARTTLS delivery. Feature flags are fixed at startup to prevent configuration reload bypass. See ACCOUNT_LIFECYCLE.md.

## Authenticator MFA

AuthenticatorService owns enrollment and MFA state; AuthService enforces the second factor at Login. AuthenticatorReplayGuard persists unique per-user/secret TOTP digest fences in Infrastructure. Do not issue JWT before both factors. Refer to AUTHENTICATOR_MFA.md.

## WebAuthn Passkeys

Application PasskeyService calls the official .NET 10 IPasskeyHandler, and delegates credential persistence to UserManager. Domain PasskeyCeremony and Infrastructure EF SQL store its state, purpose, expiry and local account binding. Server PasskeysController exposes typed WebAuthn operations and enforces explicit local JWT or anonymous schemes, behind an opt-in Origin/RP policy. Never replace the official FIDO2 signature handler with application-level crypto. Read PASSKEYS.md.

## Typed SDKs

The API remains backend-only and framework-agnostic. The actual development Swagger document is snapshotted into openapi/v1.json by tools/export_openapi.py. tools/generate_sdks.py deterministically produces TypeScript and Dart request/response models and operation clients. Each platform owns a small, manually reviewed HTTP transport with scheme separation and rotating local token support; generated files must never be edited directly. See SDK_GENERATION.md and preserve stable API operation IDs.
