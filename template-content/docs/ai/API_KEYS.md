# Service-to-service API Keys (Phase 6.2)

API Keys authenticate **services**, not interactive human users. This feature uses an independent `ApiKey` authentication scheme and never grants the local `Administrator` role. Keys are bearer credentials: use **TLS**, keep them out of browser/PWA bundles, query strings, URLs and logs, and store them in your service's secret manager.

## Admin lifecycle

A local administrator, authenticated with a local JWT access token, can:
- `POST /api/api-keys`: body `{"name":"job-runner","scopes":["integration:read"],"validDays":30}`, returns `apiKey` **once** (201).
- `GET /api/api-keys`: last 100 metadata records only; raw credentials are **never** returned.
- `DELETE /api/api-keys/{id}`: immediate revocation (204), never hard-deletes the audit metadata.

The default scope allowlist is intentionally small: `integration:read` and `integration:write`. Validate new scope names and protect each endpoint with a corresponding policy before adding more. Keys expire within 1–365 days; there is no unlimited-lifetime default. To rotate, create a new key, deploy it, then revoke the old ID.

The generated credential is `mkx_` plus 48 cryptographically random bytes encoded base64url. Only the SHA-256 hash, the visible prefix, expiration, scope list, creator and revocation metadata are stored in SQL Server. Creating a key requires an existing local Administrator. The initial admin is bootstrapped only when explicitly enabled through secrets.

## Calling APIs

```http
GET /api/integrations/ping
X-API-Key: mkx_<secret>
```

The example `GET /api/integrations/ping` requires `integration:read`. A missing or invalid key receives 401; a valid key without this scope receives 403. Revoked or expired keys receive 401. The service's authentication identity consists only of its key ID, client name and scopes—**no local user identity or roles**. Passing an API key to a local AdminOnly endpoint cannot grant privileges.

The default `JwtBearer` and optional `ExternalOidc` authentication modes remain separate. API key auth is not used to sign JWTs. Rotate credentials when suspected compromised. Monitor invalid attempts and implement production-specific gateway limits. Do not make API Keys available to untrusted web/mobile clients.

The initial migration for API key storage is included in the template. Apply migrations before enabling administrative key creation and integration requests.
