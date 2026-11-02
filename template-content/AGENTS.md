# Mkx.Templates.Api — Generated application agent guide

This is a backend-only .NET 10 solution. Read `docs/ai/ARCHITECTURE.md` and `docs/ai/OPERATIONS.md` before changes.

- Preserve the pragmatic Mkx.Templates.Blazor backend boundaries: Domain (entities and invariants), Infrastructure (EF Core and Identity stores), Shared (DTOs/routes), Application (use cases), Server (HTTP/JWT/hosting).
- No Blazor, SSR, WASM, CQRS, MediatR, forced repository/unit-of-work wrappers or client dependencies.
- Controllers delegate to Application. Do not put business behavior or DbContext directly in controllers.
- Enforce permissions on the server; client-side visibility is never authorization.
- JWT access tokens are short-lived and signed; opaque refresh tokens are hashed, rotated atomically and revocable.
- No hardcoded credentials, signing keys, production DB strings or generated bin/obj files in Git.
- Run `dotnet build Mkx.Templates.slnx` and `dotnet test --project Tests/Mkx.Templates.Tests/Mkx.Templates.Tests.csproj` from the solution directory. Keep docs and tests aligned with behavior.

- Keep ExternalOidc, ApiKey and local JWT schemes independent. AdminOnly must remain local JWT only. Do not accidentally grant API keys local user roles. All new integration scopes require a reviewed allowlist change and matching server policy.

- MANDATORY: before adding or changing an endpoint, read docs/ai/OPENAPI.md; maintain the same themed Swagger, grouped module tags, stable Controller_Action IDs, explicit response DTOs/status codes and correct LocalJwt/ExternalOidc/OpaqueToken/ApiKey security. Add/extend OpenApiContractTests. Do not leave endpoints under 99 Other or expose Swagger in production. Opaque introspection never grants local roles.

- Maintain account-link two-party proof: signed-in local nonadmin starts a short-lived hashed challenge, validated human external token with exact issuer/scope consumes it once; no email/name/national-ID auto-link, no administrator or MFA bypass, no local JWT issuance from external identity. Read docs/ai/ACCOUNT_LINKING.md.

- For onboarding changes, read docs/ai/ACCOUNT_LIFECYCLE.md. Keep USERNAME login, confirmed account required, email-only delivery of codes, persistent DP keys, opt-in SMTP, generic recovery responses, source-IP throttling and zero tokens in logs. Do not enable MFA flag before implementing an actual challenge protocol; administrator recovery remains excluded.

- MFA guardrail: read docs/ai/AUTHENTICATOR_MFA.md. Never issue JWT for MFA-enabled users without password plus validated TOTP or a single-use recovery code; enforce replay guard and refresh revocation when MFA changes; keep local JWT management isolated from external OIDC and API key schemes; do not log setup keys or recovery codes.

- Passkey changes: read docs/ai/PASSKEYS.md. Keep the API-only .NET 10 built-in IPasskeyHandler (never hand-roll WebAuthn verification), explicit RP ID and exact browser Origin, opt-in secure startup, Identity v3 AspNetUserPasskeys table and server-side SQL PasskeyCeremony with atomic one-time consumption. Registration requires signed-in local account plus password and any existing MFA; passkey assertion may issue local JWT only after cryptographic validation and mandatory TOTP/recovery on MFA accounts. Keep swagger scheme tests, domain migrations and SQL smoke in sync.

- SDK contract guardrail: read docs/ai/SDK_GENERATION.md. Any HTTP contract change requires updating openapi/v1.json through tools/export_openapi.py, regenerating clients via tools/generate_sdks.py, and preserving TypeScript/Dart tests. Do not manually edit generated files, silently add a security-scheme fallback, hardcode credentials, or auto-retry non-idempotent operations. Preserve username-based local login and single-flight rotating refresh; never expose service API keys to public clients.
