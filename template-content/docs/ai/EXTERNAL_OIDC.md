# Optional external OIDC / JWT access-token validation (Phase 6.1)

This is a **resource-server** integration. The API accepts JWT **access tokens** from a trusted OpenID Connect / OAuth provider; it is **not** an OAuth authorization server, does not issue external tokens, and does not automatically import/map users.

Configuration (environment variables are recommended for deployment):

- `ExternalOidc__Enabled=true`
- `ExternalOidc__Authority=https://identity.example.com`
- `ExternalOidc__Audience=your-resource-api`

The authority MUST be HTTPS and publish a standards-compliant discovery document and JWKS containing the signing keys. The audience must match the API resource identifier in the *access token*, not a mobile/web client's ID token audience. ASP.NET JWT Bearer validates the issuer, audience, signing signature, expiry, and keys discovered from the provider. Requests to `GET /api/auth/external/me` require only this external scheme and return the validated `sub` and `iss` as a diagnostic example.

In a React Native / Flutter app use **Authorization Code + PKCE** with the **system browser**, registered redirect URIs, and a provider-supported native OAuth client. The native app obtains an access token for the configured API audience and includes `Authorization: Bearer <access_token>` when calling an external-OIDC-protected API endpoint. External identity schemes must be explicitly chosen by a controller or authorization policy; they do not silently impersonate local ASP.NET Identity users.

The local `POST /api/auth/login` and `POST /api/auth/refresh` still use the original JWT scheme. The `AdminOnly` policy explicitly uses **local JWT Bearer only**; external IdP roles do not automatically grant local administrator privileges.

When external OIDC is disabled (default) the named scheme returns 401 and never trusts an external token. Enabled with missing values or an HTTP authority fails startup. The authority must be reachable from the API server; no default BirSign URL or credential is embedded.

**Integration caveats:** Many OIDC providers issue opaque access tokens rather than JWT, or issue JWTs with an audience that differs from a web client ID. This implementation requires a JWT **access token** with the right audience and HTTPS discovery. Do not put ID tokens in API authorization headers. Opaque token introspection, external account provisioning/role mapping, multi-provider selection and self-hosted OpenIddict remain later optional work. Validate the real BirSign/Keycloak token contract and end-to-end HTTPS discovery before claiming production federation is proven.

## Local BirSign compatibility and scope authorization

ExternalOidcOnly requires the configured ExternalOidc:RequiredScope (default mkx.api.read). A correctly signed JWT with correct issuer/audience but without this exact scope is rejected. Claims may be space-delimited. Machine-to-machine client_credentials tokens may contain client_id but no user sub; /api/auth/external/me supports either identity without assigning local administrator roles. Real local Duende/BirSign client_credentials tests are documented in the parent template repository docs/LOCAL_BIRSIGN_INTEGRATION.md. Production and interactive SSO were not tested.
