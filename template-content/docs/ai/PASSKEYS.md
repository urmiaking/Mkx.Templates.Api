# Passkeys / WebAuthn - optional API-only foundation (Phase 7.3)

This template uses ASP.NET Core Identity .NET 10 built-in WebAuthn cryptographic handling (IPasskeyHandler<AppUser>) and the EF Core Identity passkey store. The API issues normal local JWT/rotating refresh tokens only after a signed assertion is verified. No Identity cookie, frontend, or homemade FIDO2 verifier is bundled.

## Configure the Relying Party before enabling

Passkeys are DISABLED by default. For a frontend at https://app.example.com and a trusted relying-party domain example.com, configure environment settings:

    Passkeys__Enabled=true
    Passkeys__RelyingPartyId=example.com
    Passkeys__Origin=https://app.example.com

The browser origin must match exactly on credential registration AND authentication. The RP ID must be the origin hostname or a valid parent DNS domain. Invalid combinations fail startup; HTTPS is mandatory except loopback localhost/127.0.0.1. If the frontend differs from the API host, configure the exact browser origin under Cors__AllowedOrigins. Restrict proxy Host/Forwarded headers. Do not use wildcard origins.

Mobile deployments require their own associated-domain/asset-link policies and platform authenticator integration. This backend does not configure Apple/Android app association or provide a mobile UI.

Apply the AddPasskeyCeremonies EF Core migration to the intended SQL Server first. It creates AspNetUserPasskeys (Identity v3 credential public keys and counters) and PasskeyCeremonies. Store and back up the database securely.

## Registration of an existing verified local user

1. With a LOCAL JWT, POST /api/passkeys/register/options with JSON Password. If the account already has TOTP MFA, also supply AuthenticatorCode or a one-time RecoveryCode. The API rechecks password and required second factor before enrollment.
2. Response contains CeremonyId UUID and OptionsJson (a JSON STRING). In a secure browser origin, parse OptionsJson using PublicKeyCredential.parseCreationOptionsFromJSON then call navigator.credentials.create with the resulting publicKey options.
3. POST /api/passkeys/register/complete with the same LOCAL JWT, CeremonyId and CredentialJson (JSON.stringify of the browser PublicKeyCredential). The native Identity verifier validates the origin, RP, challenge, presence, verification and attested public key. The service binds the attested user to the signed-in user ID and unchanged security stamp. No client-controlled attestation state is trusted.

## Discoverable passkey login

1. Anonymous POST /api/passkeys/login/options returns CeremonyId and OptionsJson, without asking for or revealing a username. Passkeys require discoverable resident keys.
2. Parse OptionsJson with PublicKeyCredential.parseRequestOptionsFromJSON and call navigator.credentials.get in the trusted browser origin.
3. Anonymous POST /api/passkeys/login/complete with CeremonyId and CredentialJson. The .NET Identity verifier checks the signed assertion, challenge, RP, origin and authenticator counter, then updates the verified passkey. The API issues the usual local JWT and refresh tokens only on success.
4. If the local account has TwoFactorEnabled, the assertion alone is not sufficient: ALSO supply AuthenticatorCode or RecoveryCode. A passkey is a primary authentication factor, not a bypass for configured MFA.

## Management and replay

GET /api/passkeys with LOCAL JWT lists only credential IDs and public labels. POST /api/passkeys/remove with LOCAL JWT requires CredentialId, current Password and (if MFA is enabled) AuthenticatorCode or RecoveryCode. A removal updates Identity security stamp and revokes refresh sessions; existing short-lived access JWTs remain valid until expiration.

The server alone stores every challenge, purpose, optional registration owner, original security stamp, 3-minute expiration, and consumed marker. Consumption uses an atomic SQL update; a challenge cannot be redeemed twice or for a different user/purpose. Expired challenges are rejected and older abandoned entries are pruned when issuing new options. Do not log credential assertions, secrets, recovery codes or ceremony state. Public login is rate limited.

## Verification limits

Identity SQLite integration tests exercise native handler options, passkey storage schema, ceremony ownership, invalid attestation, replay rejection, expiry/pruning, MFA enrollment protection, default-off mode and origin policy. Real disposable SQL Server HTTP smoke verifies public and authenticated options, invalid-attestation replay protection, migration and existing session behavior.

Not yet verified: a successful, signed end-to-end WebAuthn registration and login with Windows Hello, Touch ID, Face ID, hardware key, or a virtual authenticator; actual mobile app association; and production domain configuration. Before using this in production, complete a real frontend/browser/device interoperability test. The API-only template does not generate a UI or mobile application.
