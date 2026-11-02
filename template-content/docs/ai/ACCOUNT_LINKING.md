# External user linking and native PKCE

This is a resource-server template, not an OAuth authorization server. A native, web or desktop client uses the system browser, Authorization Code and PKCE (S256), validates state and nonce, then sends an ACCESS token for the API audience, never an ID token.

## Local BirSign setup

Run scripts/birsign-local-pkce.sql with sqlcmd against local Windows SQL Server BirSignDb (integrated authentication only). The isolated public client mkx.api.local.pkce uses authorization_code only, RequirePkce=true, no plaintext verifier and no client secret. Scopes are openid, profile and mkx.api.read. Redirect http://127.0.0.1:52173/callback is only for a LOCAL loopback smoke test; register a real platform callback for React Native or Flutter.

When STS is running on https://localhost:44310, scripts/birsign-local-pkce-smoke.ps1 verifies a correct PKCE authorization request redirects with HTTP 302 to /Account/Login. This proves only pre-login authorization, not code redemption or human sign-in.

## Two-party external identity link API

POST /api/auth/external-links/challenge is LOCAL JWT only. A confirmed nonadmin local Identity user requests a 48-byte unpredictable challenge, returned only once, stored as SHA-256 hash, expiring after 5 minutes. Never put it in URLs, logs or analytics.

POST /api/auth/external-links/complete is EXTERNAL OIDC JWT only. The same person first obtains an external USER access token using PKCE and then submits that challenge in a JSON body. The verified external token must have a real sub and the required scope mkx.api.read. Client_credentials tokens without a human sub cannot link. The challenge is consumed atomically and cannot be reused.

GET /api/auth/external-links/me is EXTERNAL OIDC JWT only and returns the linked local profile, never a local JWT or local roles. DELETE /api/auth/external-links is LOCAL JWT only and removes the mapping for the configured issuer. Distinct unique DB indexes prevent one external issuer-subject linking to multiple local accounts or multiple subjects under the same issuer linking to one local account.

Admin, unconfirmed, locked and MFA-enabled local users cannot link or resolve an external mapping. The code does not match by email, username, national ID or display name and never grants local Administrator. Apply migration AddExternalIdentityLinks to existing databases. Self-registration and automatic local JWT issuance are outside this phase.

## Verification limits

A real LOCAL BirSign PKCE authorization request was accepted and redirected to the login page. Link completion, one-time proof and uniqueness tests use EF Core SQLite and test identity principals. Human typing credentials, receiving an authorization code, and redeeming that code were NOT automated, nor was production BirSign changed.
