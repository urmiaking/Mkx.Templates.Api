# Setup and operations

1. Supply ConnectionStrings__DefaultConnection for SQL Server; use a restricted account in production.
2. Supply Jwt__SigningKey with a random secret of at least 32 UTF-8 bytes. Configure Jwt__Issuer and Jwt__Audience for the environment; never commit secrets.
3. Create/review initial migration with:
   `dotnet ef migrations add InitialIdentity --project src/Core/Mkx.Templates.Infrastructure --startup-project src/Server/Mkx.Templates.Server --output-dir Persistence/Migrations`
   Apply with `dotnet ef database update --project src/Core/Mkx.Templates.Infrastructure --startup-project src/Server/Mkx.Templates.Server` only against the intended database. Automatic migrations disabled by default.
4. Optional one-time admin: configure BootstrapAdmin__Enabled=true, BootstrapAdmin__Username and BootstrapAdmin__Password securely. After successful startup, disable the flag and remove password from environment. Existing accounts are never overwritten or promoted.
5. Development-only Swagger /swagger, health endpoints /health/live (process) and /health/ready (database).
6. Add explicit Cors__AllowedOrigins__0 etc for browser clients. Native mobile apps do not require browser CORS.
7. Store mobile tokens in platform secure storage. Local login is appropriate only for trusted first-party clients over TLS; for general third-party mobile clients implement OAuth authorization code + PKCE as a separate optional phase.
8. In production configure TLS termination, proxy trust, secret rotation and audit/monitoring appropriate to deployment.

Known limits: no self-service registration, password recovery, SMS/OTP, passkeys or OIDC provider in this first version. Logout invalidates refresh tokens but already-issued access JWTs remain valid until expiry.

## Authorization and rate limits

- Authorization is required by default for new API routes (fallback policy). To make a public route, explicitly mark it [AllowAnonymous], review its exposure, and test it.
- /health/live and /health/ready are explicitly anonymous so infrastructure probes can work. They return health status only. Swagger is available only in Development.
- Auth endpoints use a per-remote-IP rate limiter. The default RateLimit:AuthRequestsPerMinute is 10, configurable from environment/secret-managed configuration. Set trusted reverse-proxy forwarding deliberately; do not blindly trust client-supplied X-Forwarded-For.
- A second use of a rotated refresh token invalidates the entire refresh family. Logout and logout-all invalidate refresh tokens; already issued access JWTs remain valid until expiry.
- MFA-enabled users require password plus a verified six-digit TOTP or one-time recovery code at login. MFA-verified refresh sessions are allowed to rotate, and old sessions are revoked on MFA state changes.

## Optional external OAuth/OIDC
See [External OIDC resource-server configuration](EXTERNAL_OIDC.md). Disabled by default, validates HTTPS metadata and API access-token audience. Neither local account linking nor OAuth token issuance is provided.

## Service-to-service API keys
See [API key lifecycle and security](API_KEYS.md). An administrator can issue a scoped, expiring key; the raw secret is returned once. Apply the included AddApiClientKeys EF migration to existing databases and keep keys in the consumer service secret manager. Do not embed them in mobile/web clients.

## Structured Swagger & opaque tokens

## External account linking and native client PKCE

Follow ACCOUNT_LINKING.md. AddExternalIdentityLinks migration is required for existing SQL Server installations. No automatic local JWT or Administrator roles are issued from external identity; the client handles PKCE using the system browser.

## Opt-in username registration, email verification and password recovery
See ACCOUNT_LIFECYCLE.md. Feature disabled by default; enabling requires persistent Data Protection keys and authenticated STARTTLS SMTP secret configuration. No session before email confirmation. SMTP delivery remains unverified without real provider credentials. Full MFA and passkeys remain future phases.

When enabling onboarding during first-time BootstrapAdmin creation, supply BootstrapAdmin__Email to preserve the unique-email Identity requirement; see ACCOUNT_LIFECYCLE.md.

## Authenticator MFA

See AUTHENTICATOR_MFA.md. Apply migration AddAuthenticatorReplayFence to existing installations before enabling MFA. Never expose authenticator secrets or recovery codes in logs. MFA users must supply a second factor at login; already-issued access JWTs expire naturally, while refresh sessions are revoked on MFA state changes.

## WebAuthn Passkeys

See PASSKEYS.md. Disabled by default: enabling requires trusted Passkeys__RelyingPartyId and Passkeys__Origin, HTTPS except loopback, and the AddPasskeyCeremonies SQL migration (including AspNetUserPasskeys). The authenticated browser frontend origin is verified exactly. Device/mobile associated-domain tests are still required before production sign-off. No passkey state is kept client-side; the API preserves MFA checks.
