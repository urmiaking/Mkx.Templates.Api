# API template maintenance

When editing `Mkx.Templates.Api`, inspect `template-content/AGENTS.md`, `docs/ROADMAP.md`, and relevant `template-content/docs/ai/` first. The entire application that `dotnet new` produces lives under `template-content`; root is only NuGet packaging, CI and delivery docs. Keep architecture aligned with Mkx.Templates.Blazor's pragmatic backend layers (Domain, Infrastructure, Shared, Application, Server) without copying UI dependencies. Each delivery phase must update `docs/ROADMAP.md` and `docs/PHASE_LOG.md` with honest evidence; verify generated project and package, not just source. Avoid embedding any secrets.

For API route changes read template-content/docs/ai/OPENAPI.md and .matemcp/skills/openapi-contract/SKILL.md, then validate the generated OpenAPI JSON, UI CSS and packaged template tests.

External account linking rule: follow template-content/docs/ai/ACCOUNT_LINKING.md. Keep the one-time hashed proof, explicit issuer+sub identity, exact external scope, distinct JWT schemes, local admin exclusion and migration/security regression tests.

Account lifecycle guardrail: consult template-content/docs/ai/ACCOUNT_LIFECYCLE.md, preserve opt-in SMTP, persistent Data Protection key storage and username-only login. No unconfirmed token issuance, email-code logging, administrator anonymous recovery, or bypass of existing MFA fail-closed logic.

Authenticator MFA guardrail: see template-content/docs/ai/AUTHENTICATOR_MFA.md. Maintain TOTP replay fence, unique SQL constraint, stamp rotation/refresh revocation, LocalJwt-only management and MFA tests. SMS OTP and passkeys remain out of scope.

Passkeys/WebAuthn guardrail: read template-content/docs/ai/PASSKEYS.md. The builtin .NET 10 Identity WebAuthn handler performs cryptographic validation, and SQL PasskeyCeremony holds purpose/user/stamp-bound, 3-minute, one-use state. Explicit trusted RP/Origin opt-in, local JWT management and usernameless anonymous login must remain distinct. Preserve existing TOTP MFA requirement for MFA-enabled accounts and never issue a JWT before handler verified assertion. Update Identity v3 passkey migration, security/HTTP tests, SQL smoke, generated package and documentation with changes.
