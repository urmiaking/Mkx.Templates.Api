# Authenticator TOTP MFA and one-time recovery

This API supports authenticator-app RFC6238 six-digit TOTP. It does not implement SMS or email login OTP. Optional WebAuthn/passkeys are documented separately in PASSKEYS.md; users with TOTP MFA enabled must also verify that second factor after a passkey assertion. Username remains the login identity. All MFA management endpoints require a local Identity JWT, never ExternalOidc, opaque tokens or API keys. Requests are rate-limited; sensitive responses have no-store.

## Enrollment

1. GET /api/auth/authenticator/status: return enrolled status and recovery-code count (no secrets).
2. POST /api/auth/authenticator/setup with JSON Password: reauthenticate a confirmed, unlocked, non-MFA account; rotate the pending authenticator secret and return SharedKey and otpauth provisioning URI. Never store, log or embed these in analytics or query parameters. The caller must securely present the QR and keep it in memory only.
3. POST /api/auth/authenticator/enable with a six-digit AuthenticatorCode: verify the TOTP, consume it using the persistent replay guard, enable MFA, rotate the security stamp, revoke all refresh sessions, and return eight one-time recovery codes ONCE. Ask the user to store recovery codes offline; never log them. After enabling, wait for the NEXT fresh TOTP before logging in again; the enrollment code is already consumed.
4. POST /api/auth/authenticator/disable with Password and either AuthenticatorCode or RecoveryCode: reauthenticate with both factors, rotate the authenticator key and security stamp, revoke refresh sessions and turn MFA off.

## Sign-in

POST /api/auth/login still uses Username and Password. For accounts with TwoFactorEnabled, LoginRequest additionally requires exactly one AuthenticatorCode (six digits) or RecoveryCode. Password-only attempts return HTTP 401 and issue NO partial JWT. Incorrect factors increment Identity failed attempts and can lock the account. ASP.NET Core Identity manages recovery-code one-time redemption.

MFA-verified refresh sessions rotate normally; sessions minted before changing MFA state fail by security stamp and revocation. Existing short-lived access JWTs remain usable until expiration (15 minutes by default) because the template does not maintain a real-time JWT denylist. Do not claim immediate access-token invalidation.

## Single-use TOTP replay protection

Migration AddAuthenticatorReplayFence stores (UserId, SHA256 digest) with a unique key and ExpiryUtc. Each digest binds the six-digit code to the user and the current authenticator secret. No raw code is stored. A valid code can be consumed once across API instances, including enrollment, login and disablement. Three-minute fences are lazily purged per user; concurrent duplicate requests fail on the SQL uniqueness constraint. Enroll and log in on different TOTP time steps; a code cannot be reused while otherwise within the normal clock-skew window.

Protect the Identity SQL Server and its user tokens/keys with encrypted backups, least privilege and transport encryption. Keep authenticator devices time synchronized.

## Verification and limitations

Tests exercise real Identity TOTP enrollment and validation using generated RFC6238 codes, wrong password, wrong OTP, failed login without second factor, single-use recovery code, TOTP replay rejection, lockout/confirmation exclusions, refresh revocation on MFA changes, and anonymous HTTP route denial. Source and generated OpenAPI tests verify stable Authenticator operation IDs and LocalJwt security. Disposable SQL Server smoke applies the migration and verifies existing login/refresh/API key protections. A standalone end-to-end SQL Server MFA HTTP test and physical authenticator-device test were not executed. SMS/email login OTP remain planned. Passkey backend support is available as opt-in per PASSKEYS.md, but live authenticator devices still need testing.

For high-risk apps, consider audited step-up authentication and explicit access JWT invalidation as separate extensions; never infer MFA from external IdP claims without an explicitly reviewed policy.
