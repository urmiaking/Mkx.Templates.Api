# Optional opaque OAuth access token introspection (Phase 6.3)

This optional adapter validates opaque **access tokens** against a trusted provider's OAuth 2.0 Token Introspection endpoint (RFC 7662). It does not issue tokens, manage the upstream authorization process, accept ID tokens, or create local ASP.NET Identity user records.

Set these in an environment/secret manager:

```
OpaqueIntrospection__Enabled=true
OpaqueIntrospection__Endpoint=https://id.example.com/connect/introspect
OpaqueIntrospection__Issuer=https://id.example.com
OpaqueIntrospection__Audience=my-resource-api
OpaqueIntrospection__ClientId=resource-api-introspection-client
OpaqueIntrospection__ClientSecret=<secret FROM SECRET MANAGER>
```

All fields are required when enabled. Endpoint and Issuer must be absolute HTTPS URIs; HTTP or missing credentials reject application startup. The provider must support a confidential introspection client authenticated using HTTP Basic and accept a POST form body containing `token` and `token_type_hint=access_token`. No credentials are committed.

The independent `OpaqueIntrospection` scheme accepts `Authorization: Bearer <opaque_token>` only on actions explicitly protected by `ExternalOpaqueOnly`. The sample `GET /api/auth/opaque/me` returns validated `sub` and `iss`. Responses must have `active: true`, matching `iss` and API `aud`, a nonempty `sub`, and an unexpired `exp`. An optional `nbf` cannot be in the future. A provider that omits these claims is intentionally rejected; adapt the contract only with documented, tested provider-specific behavior. The handler limits HTTP timeouts, does not follow redirects, and never logs token or introspection client credentials.

**Security separation:** external opaque principals cannot access local Administrator policies or consume a local Identity session. No bearer token cache is used; revocations are checked on each introspected request. The scheme is disabled by default and rejects requests without network access. When the provider is down, authentication fails closed (401); monitor provider outages separately.

Use this only with a trusted IdP that exposes a real HTTPS introspection endpoint. For JWT access tokens with JWKS discovery, use `ExternalOidc` instead. Both schemes are independent from the service `ApiKey` scheme.

Integration tests validate active, inactive, wrong issuer/audience, missing and expired tokens against a test double. **Live BirSign/Keycloak introspection and OpenIddict server behavior are not claimed**. A self-hosted authorization server would require registered client management, persistence, secure signing/encryption keys, login/consent UX and audited authorization code + PKCE processing. Do not enable a partial server without these prerequisites.
