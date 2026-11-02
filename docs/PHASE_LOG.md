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

## 2026-11-02 · Phase 5.1 — Completed: Stable API baseline

- Recovered a clean main branch at 4db4c1a; prior GitHub CI for initial source/docs was green.
- Hardened refresh-token handling: a rotated token replay invalidates the complete family, including a losing competing refresh request. Kept EF SQL retry execution strategy and atomic update transaction.
- Added fallback authorization: new endpoints require authentication unless marked AllowAnonymous. Accounts with TwoFactorEnabled are denied local login/refresh until MFA is actually supported. Login/refresh and live/ready health endpoints are explicitly public.
- Auth IP rate limiting is configurable (RateLimit:AuthRequestsPerMinute, default 10; local integration smoke overrides to 60 only in that process).
- Tests expanded from six to eight, including authorization fallback and anonymous malformed login validation.
- Real SQL smoke passed LOGIN, ME, ADMIN_POLICY, REFRESH, REPLAY, REPLAY_FAMILY_REVOCATION, RELOGIN, LOGOUT, REVOKE, LOGOUT_ALL and READY; ephemeral database dropped successfully.
- NuGet template package version raised to 0.1.1 and successfully installed from the .nupkg. Generated Stable.Smoke solution built with 0 warnings/errors; all 8 generated tests passed. GitHub CI for commit 48f9ce0 (run 41360215260) passed the application build, tests, NuGet pack, generated solution build and tests. Updated checkout/setup-dotnet actions to the latest stable GitHub release majors (v7/v6); verify these in the next CI run before the release.

Scope remains API-only Identity + JWT. OIDC, passkeys, SMS/OTP, account recovery and generated mobile SDKs are still planned, not delivered.
### GitHub handoff (completed)

- Main branch stabilization commit: 48f9ce0. GitHub Actions run https://github.com/urmiaking/Mkx.Templates.Api/actions/runs/41360215260 succeeded.
- CI maintenance commit: 6073b30, updating checkout to v7 and setup-dotnet to v6. Final workflow https://github.com/urmiaking/Mkx.Templates.Api/actions/runs/41360362576 succeeded including source build, source tests, package generation, generated app build and generated app tests.
- GitHub v0.1.1 release: https://github.com/urmiaking/Mkx.Templates.Api/releases/tag/v0.1.1, with attached Mkx.Templates.Api.0.1.1.nupkg; package is installed and usable with dotnet new mkx-api.
- Local working tree clean and no secrets or build outputs intentionally committed. Phase 5.1 finished; Issues #1-3 remain future scope.
## 2026-11-02 - Phase 6.1: External OAuth/OIDC resource-server authentication

Added independent named JWT bearer scheme ExternalOidc, with HTTPS Authority and configured API Audience. The API relies on standard provider discovery/JWKS verification, does not create local users and never maps external roles into local Administrator privileges. GET /api/auth/external/me accepts only the external scheme; local login/refresh continue unchanged. Disabled external provider fails closed (401); enabled provider with HTTP/missing Authority or Audience fails startup. Configuration is resolved during options construction to respect runtime/config overrides. Documented the provider access-token and native PKCE requirements under template-content/docs/ai/EXTERNAL_OIDC.md.

Verification: 12/12 local tests, 0 warning/error build. No live BirSign or other IdP credential supplied; real external token/JWKS integration is NOT yet tested. OpenIddict authorization server, multiple providers, opaque token introspection and account linking remain future work.
## 2026-11-02 - Phase 6.2: Service API keys (implemented and locally verified)

- New ApiClientKey entity and EF Core migration AddApiClientKeys (schema change must be applied to an existing DB before use).
- Scoped independent ApiKey authentication handler (X-API-Key), not an ASP.NET Identity account; service principals carry only api_key_id, client_name and explicit scope claims, never a local admin role.
- Local Administrator-only POST/GET/DELETE /api/api-keys; returns a cryptographically random bearer key only once on creation. DB stores SHA-256 hash, 12-char prefix, creator, allowed scopes, validity and revocation metadata.
- Key validity bounded to 1..365 days. Scope allowlist: integration:read and integration:write; example GET /api/integrations/ping requires integration:read. Wrong scope yields 403; missing/revoked key yields 401. No secrets in appsettings.
- Real SQL Server smoke verified API_KEY_CREATE, API_KEY_USE, API_KEY_NOT_DISCLOSED, API_KEY_SCOPE_DENIED, API_KEY_REVOKE, plus existing login/refresh/replay/logout-all/health checks. Temporary test DB was dropped (cleanup exit 0).
- The source and generated-template builds pass without warnings. 17/17 local tests and 17/17 tests in a new Phase6.Smoke solution instantiated from packaged Mkx.Templates.Api.0.2.0.nupkg passed.
- See template-content/docs/ai/API_KEYS.md and EXTERNAL_OIDC.md. This completes the resource-server + API-key baseline, but does NOT provide an OAuth authorization server or tested live BirSign federation. 6.3 remains planned; GitHub Actions run https://github.com/urmiaking/Mkx.Templates.Api/actions/runs/41471177480 passed Build, source tests, NuGet packaging, and generated-template build/tests. Published https://github.com/urmiaking/Mkx.Templates.Api/releases/tag/v0.2.0 with attached Mkx.Templates.Api.0.2.0.nupkg. No live external IdP integration test or OAuth authorization-server implementation is claimed; Phase 6.3 stays open.
## 2026-11-02 - Phase 6.3a and 6.4: opaque introspection and Swagger portal

- Optional RFC 7662 OAuth access-token introspection over HTTPS with HTTP Basic confidential client, strict active/issuer/audience/exp/sub verification, optional nbf, 6-second timeout and no redirects. Default disabled, fails closed, never grants local user roles. Test-double coverage, but no real BirSign/Keycloak endpoint validated.
- Rebuilt Swagger UI in Development with self-hosted dark responsive CSS, semantic groups and meaningful summaries; stable Controller_Action operation IDs, accurate authentication scheme requirements, request/response DTO and status-code metadata. Production Swagger and custom assets disabled.
- Fixed Microsoft.OpenApi 2.x empty-security bug by using OpenApiSecuritySchemeReference(scheme, context.Document). Tests validate the generated JSON contract, security and UI files.
- Added template-content/docs/ai/OPENAPI.md and OPAQUE_INTROSPECTION.md, template AGENTS.md guidance and .matemcp OpenAPI skill for future agents.
- 28/28 local tests passing, zero warnings/errors source build. NuGet 0.3.0 was packed and installed; generated Portal.Smoke solution built with zero warnings/errors and passed 28/28 generated tests. Local disposable SQL Server smoke passed LOGIN, ME, API_KEY_CREATE, API_KEY_USE, API_KEY_SCOPE_DENIED, API_KEY_REVOKE, REFRESH, REPLAY, LOGOUT and LOGOUT_ALL plus readiness, with database cleanup exit 0. GitHub CI/release evidence to follow.
- A self-hosted OpenIddict authorization server and external account linking remain deferred pending a complete secure login/consent and PKCE design. The delivered API is a resource server, not an OAuth issuer.

### Phase delivery verification

- Commit 8f21280 pushed to main. GitHub Actions https://github.com/urmiaking/Mkx.Templates.Api/actions/runs/41475844059 completed successfully with source build, 28 automated tests, NuGet packaging, generated-solution build and its 28 tests.
- Published non-draft GitHub Release https://github.com/urmiaking/Mkx.Templates.Api/releases/tag/v0.3.0 with attached Mkx.Templates.Api.0.3.0.nupkg. Package installed and generated Portal.Smoke solution built and passed tests without errors.
- Real SQL Server smoke verified existing authentication, replay/rotation and API-key use/revoke policies; temporary database cleanup exit code 0.
- Swagger/API documentation issue #5 can be closed. Issue #1 stays open solely for optional OpenIddict authorization-server/account-linking and live IdP verification; there is no hidden partially enabled authorization server.

## 2026-11-02 — Phase 6.3c: Live local BirSign test

- Verified BirSign Development STS uses localhost/BirSignDb. No remote/production changes. Local STS started at https://localhost:44310; discovery and JWKS worked.
- Added isolated SQL Client mkx.api.local.smoke, ApiScope mkx.api.read and ApiResource mkx.api transactionally only after confirming identifiers absent; SHA256/Base64 secret hash persisted to BirSignDb and protected raw secret stored in Windows user DPAPI. Provisioner rerun returned ALREADY_PRESENT; no duplicate records.
- Issued real client_credentials JWT from local BirSign: LIVE_BIRSIGN_TOKEN=PASS. Ran Mkx API at http://localhost:5287 with the validated Authority/Audience/RequiredScope. LIVE_BIRSIGN_TO_MKX_API=PASS (200); EXTERNAL_CANNOT_ACCESS_LOCAL_ADMIN=PASS (401).
- Added five OIDC scope regression tests; source build had zero warnings/errors and 33/33 tests passed.
- Local BirSignDb is approximately 38 GB, so no full backup was made; insertion was isolated, transactional and rollback-safe. No user SSO or production federation is claimed. Packaged-template, CI and release checks follow.

- The v0.3.1 NuGet package installed successfully. Fresh generated BirSign.Smoke solution built with zero warnings/errors; 33/33 generated tests passed. Both temporary local test hosts were stopped after the live integration. GitHub CI and release verification follow.

### Verified delivery: v0.3.1
- Commit d72f648: GitHub Actions https://github.com/urmiaking/Mkx.Templates.Api/actions/runs/41480571413 passed source/build, 33 tests, pack, generated-solution build and 33 generated tests.
- Release https://github.com/urmiaking/Mkx.Templates.Api/releases/tag/v0.3.1 published with Mkx.Templates.Api.0.3.1.nupkg. Local STS and API test sessions closed; Issue #1 retains optional real-user SSO/authorization-server work.

## 2026-11-02 - Phase 6.3d: public client PKCE and secure external user binding

- Added a separate public native-client registration mkx.api.local.pkce in localhost/BirSignDb using scripts/birsign-local-pkce.sql: authorization_code only, S256 PKCE required, no client secret, loopback-only callback and exactly openid/profile/mkx.api.read scopes. Isolated SQL transaction, idempotent second run, no modification to existing or production clients.
- The REAL local Duende STS discovery and authorize endpoint accepted a PKCE S256 request and redirected HTTP 302 to /Account/Login. The user login step and the code-for-token exchange were not exercised; no user passwords were available or used.
- Added ExternalIdentityLink / ExternalLinkChallenge schema and AddExternalIdentityLinks migration; Application service requires two independent proofs: valid LOCAL JWT to mint an unguessable, hashed, five-minute, one-time link challenge, and valid EXTERNAL JWT with a human subject plus API scope to consume it. API explicitly prevents automatic administrator, MFA, username/email or national-ID mapping and issues no local JWT from external access.
- Unique issuer-subject and user-issuer indexes, atomic challenge consumption and unlink support. Added separate typed OpenAPI endpoints, documented their per-operation security, extended OpenAPI contract tests, and added EF SQLite tests for replay, collision, unlink, admin and MFA restrictions.
- Local solution build 0 warnings/errors; 36/36 tests. Local ephemeral SQL Server smoke including database migration and cleanup passed. Remote CI, NuGet package and release evidence to follow.

### Delivery and verification

- Main implementation commit bbe5ae3; GitHub Actions https://github.com/urmiaking/Mkx.Templates.Api/actions/runs/41484726806 succeeded (build, 36 source tests, NuGet pack, generated project build and 36 generated tests).
- Published stable non-draft release https://github.com/urmiaking/Mkx.Templates.Api/releases/tag/v0.4.0 with Mkx.Templates.Api.0.4.0.nupkg attached. Local generated template build: zero warnings/errors, 36 tests passing. The disposable SQL Server smoke database was deleted successfully. Local BirSign authorize request returned HTTP 302 to /Account/Login with S256 PKCE; real human sign-in was not automated.
- Local BirSignDb retains only isolated public mkx.api.local.pkce registration alongside earlier isolated machine client. Local test STS was stopped. No production changes and no credentials committed.

## 2026-11-02 - Phase 7.1: Opt-in username registration and account recovery

- New account lifecycle endpoints /api/onboarding/register, /confirm, /resend-confirmation, /forgot-password and /reset-password. Uses username as login identity; email is only the confirmed delivery address. Disabled by default; when enabled, registration creates a non-confirmed Identity user and never issues local JWT. SMTP sender abstraction is configurable and uses authenticated STARTTLS; no real SMTP service was connected during this phase.
- Startup requires explicit SMTP credentials and absolute persistent Data Protection key-ring directory. The enabled flag is captured at composition; attempts to alter it during late configuration must fail or remain disabled, preventing half-enabled mode. Identity confirmation and password reset codes are valid for 20 minutes. Tokens are not returned by HTTP endpoints or placed into URLs.
- Recovery asks for matching username+email with generic 202 responses to avoid enumeration; anonymous delivery endpoints limited to 6 requests per IP per 5 minutes. Password reset uses Identity security stamp, blocking prior refresh sessions, but issued JWTs remain valid until expiry. MFA-enabled accounts remain fail-closed. Anonymous administrator password reset is disabled.
- Added typed DTO/OpenAPI documentation and fake-email-sender Identity SQLite tests for signup, confirmation, duplicate prevention, email matching, token reuse, password update, MFA restrictions; HTTP tests for disabled-by-default and incomplete SMTP config. Source build: 0 warnings/errors, 43/43 tests. Disposable SQL Server smoke passed all existing auth/API key/replay scenarios, temporary DB removed (DATABASE_CLEANUP_EXIT=0).
- No live SMTP delivery, interactive human BirSign PKCE login/code exchange, complete OTP/MFA, or passkeys is claimed. These remain future phases.

### Compatibility patch: v0.5.1

- Verified that enabling email-unique onboarding while first-time BootstrapAdmin is also active requires BootstrapAdmin__Email. Validated the email and used it for the initial confirmed administrator; no silent email assignment, no existing-account update.
- Added regression test for missing email startup rejection and valid administrator creation. 43/43 source tests and zero build warnings/errors. All existing SQL Server smoke scenarios remain compatible; final package, generated-project and CI evidence to follow.

### Release verification

- Implementation commit 0ce7861 and compatibility fix commit d91ce49 were pushed to main. GitHub Actions run https://github.com/urmiaking/Mkx.Templates.Api/actions/runs/41488496536 passed source build, 43/43 source tests, NuGet packaging, generated-solution build and 43/43 generated tests.
- Published GitHub Release https://github.com/urmiaking/Mkx.Templates.Api/releases/tag/v0.5.1 with Mkx.Templates.Api.0.5.1.nupkg attached. Local final package instantiation succeeded with 0 warnings/errors and 43 generated tests passed; disposable SQL Server smoke passed with cleanup exit code 0.
- Issue #2 stays open for OTP/MFA, passkey/WebAuthn, provider-specific SMTP delivery testing and abuse controls; these have not been marked implemented or enabled.

## 2026-11-02 - Phase 7.2: TOTP Authenticator and replay-safe MFA

- Added local JWT-only Authenticator status/setup/enable/disable operations, protected by per-IP auth rate limiting and response no-store. Setup requires password and produces a temporary secret/otpauth URI; enable verifies TOTP and returns 8 one-time recovery codes. Disabling requires password plus TOTP or recovery code.
- Local login now requires password AND either valid Authenticator TOTP or redeemed recovery code for TwoFactorEnabled users. Partial JWT/refresh is never issued. Wrong second factors count toward account lockout. MFA-verified sessions can refresh; enabling/disabling MFA rotates the security stamp and revokes all prior refresh sessions. Existing short-lived JWTs remain valid until expiration.
- Added unique SQL RedeemedAuthenticatorCodes per-user+digest replay fence (migration AddAuthenticatorReplayFence), three-minute expiry/lazy cleanup, and verification before digest consumption. This prevents reuse of a current TOTP across separate API instances.
- SQLite Identity tests cover actual RFC6238 codes, enrollment, setup password proof, replay rejection, recovery redemption, old-session revocation, disable, locked/unconfirmed restrictions; HTTP tests enforce anonymous 401 on settings. Source: 46/46 tests, zero build warnings/errors. Full SQL Server migration and existing authentication/API-key/refresh smoke passed with disposable database cleanup. Standalone full SQL MFA HTTP flow not tested.
- No SMS/email login OTP, physical device enrollment, passkeys/WebAuthn or live IdP human login claimed. Phase 7.3 remains planned.

### Phase 7.2 released: v0.6.0

- Commit 21cfffe pushed to main; CI https://github.com/urmiaking/Mkx.Templates.Api/actions/runs/41491699396 succeeded, including source build, 46 source tests, NuGet pack, generated solution build and 46 generated tests.
- Published non-draft release https://github.com/urmiaking/Mkx.Templates.Api/releases/tag/v0.6.0 with Mkx.Templates.Api.0.6.0.nupkg. Local source/generated builds zero warnings and errors; disposable SQL Server smoke migration and cleanup succeeded.
- Issue #2 remains open for SMS/email OTP, WebAuthn/passkeys and live-device/provider verification. No claim that full SQL MFA HTTP journey was exercised.

## 2026-11-02 - Phase 7.3: Opt-in .NET 10 WebAuthn/Passkeys backend

- Implemented PasskeysController with protected register/options, register/complete, list, remove and anonymous discoverable login/options, login/complete operations. Uses official .NET 10 Identity IPasskeyHandler for attestation/assertion, origin/challenge/signature verification and counter handling; no custom WebAuthn cryptography or Identity cookie login is introduced.
- Requires Passkeys Enabled=true, trusted Origin and RelyingPartyId; HTTPS except localhost/loopback. Default remains disabled and invalid configurations reject startup. Register/remove require LOCAL JWT, current password and any configured TOTP/recovery second factor. Registration verifies identity ownership and unchanged security stamp. Passwordless passkey sign-in still enforces extra second factor for TwoFactorEnabled accounts before issuing local JWT/refresh tokens. Removal revokes refresh sessions and rotates the security stamp.
- Added PasskeyCeremony SQL entity with 3-minute expiry, purpose, bound owner/security stamp for registration, single-use atomic consumption and lazy pruning of expired challenges. Upgraded the EF Identity v3 model explicitly to include official AspNetUserPasskeys storage; migration AddPasskeyCeremonies creates both tables.
- Source tests: 55/55, zero warnings/errors. SQLite tests used real Identity handler for generation, persistence and invalid credential verification, owner binding, purpose/replay/expiry/pruning and MFA exclusion; HTTP tests verify strict opt-in/Origin policy and authorization schemes. Real disposable SQL Server HTTP smoke: login/passkey options, register/options, invalid attestation rejected, challenge replay denied; all existing login/refresh/API key tests still passed, database cleanup exit 0.
- NOT verified: full signed WebAuthn attestation and assertion with a real/virtual authenticator, native mobile associated-domain integration or production origin. These remain acceptance checks before treating the feature as production-validated.

### Phase 7.3 delivery: v0.7.0

- Implementation commit 3fb70df on main; independently verified GitHub Actions https://github.com/urmiaking/Mkx.Templates.Api/actions/runs/41495598154 succeeded. This run built source, ran 55/55 source tests, packed the NuGet template, generated a new solution, built it and passed 55/55 generated tests.
- Published non-draft release https://github.com/urmiaking/Mkx.Templates.Api/releases/tag/v0.7.0 with Mkx.Templates.Api.0.7.0.nupkg asset. Local package installed successfully and generated new solution built with zero warnings/errors. Disposable real SQL Server HTTP smoke passed passkey start/register/invalid-attestation/replay, existing authentication, refresh rotation and API key checks with database cleanup exit 0.
- Issue #2 stays OPEN for a real signed WebAuthn browser/Windows Hello authenticator registration/assertion acceptance check and possible platform/mobile association, plus SMS/email OTP backlog. No live device signing success is claimed.

## 2026-11-02 - Phase 8: typed TypeScript and Dart SDKs (v0.8.0)

- Exported canonical template-content/openapi/v1.json directly from actual Development Swagger (disposable local JWT, no DB/bootstrap call) using a Python 3 stdlib script. Contract has 32 operations, 33 typed DTO/response schemas and 4 distinct security schemes.
- Added deterministic, fail-closed Python codegen to generate TS model interfaces/operation methods and Dart typed data classes/operation methods. Added runtime transports with host-supplied local session store, single-flight local JWT refresh-on-401, rotation persisted through host callback, exactly one retry, distinct external OIDC/opaque/API key credential suppliers and anonymous operations with no auth headers. No token storage inside SDK; HTTPS enforced outside loopback.
- Fixed previously untyped response schemas for ExternalIdentity_Me, OpaqueIdentity_Me, Integrations_Ping and Health_Live with explicit Shared DTOs and server OpenAPI contract assertions.
- Added React Native/Expo and Flutter integration examples using platform-secure storage and local SDK packages. No production host, client credentials or API-key material embedded. SDKs are included in generated dotnet-new template, not published to npm/pub.dev.
- Source .NET build and 55 tests passed; TS compiled and passed 7 Node tests; Dart analyzed with no warnings and passed 7 tests. CI updates check live contract snapshot, generated SDK parity and SDK tests in source and instantiated NuGet package. Package/CI/release evidence to follow; NuGet.org publication remains separate and pending publisher credentials and approval.

### Phase 8 release verification: v0.8.0

- Implementation commit 3a5b0c7 and CI runtime patch fe87b65 merged/pushed to main. Latest independent CI https://github.com/urmiaking/Mkx.Templates.Api/actions/runs/41499910376 succeeded: source .NET build and 55/55 tests, actual Swagger snapshot + TS/Dart codegen parity, TypeScript compilation and 7/7 Node tests, Dart analysis and 7/7 tests, NuGet pack, and full re-verification of a freshly instantiated template including both SDKs.
- Published non-draft GitHub Release https://github.com/urmiaking/Mkx.Templates.Api/releases/tag/v0.8.0 with Mkx.Templates.Api.0.8.0.nupkg attached (129 packaged entries, no node_modules, dist or Dart cache). On Windows local generated solution also passed: 55 .NET tests, 7 TS tests, 7 Dart tests, schema parity and zero-warning build.
- No NuGet.org, npm or pub.dev publishing occurred. GitHub repository has no configured NuGet.org publishing secret at verification time. Issue #3 stays open only for optional external registries / live client integration acceptance.
