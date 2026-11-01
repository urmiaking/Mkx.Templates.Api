# Architecture

This API-only template follows Mkx.Templates.Blazor backend's pragmatic layers: Domain -> Infrastructure -> Application -> Server (dependency direction), and Shared for DTOs and route constants. No client UI.

- Domain: AppUser, RefreshSession; no EF configuration.
- Infrastructure: AppDbContext, Identity stores, SQL Server, EF mapping and optional specifications.
- Application: IAuthService, Identity user management, credential checking and session lifecycle.
- Server: HTTP controllers, JWT issuer/verifier, bootstrap admin, health, middleware, hosting.
- Tests: API pipeline and authorization smoke checks.

Controllers are adapters; use cases live in Application; HTTP contracts stay in Shared. No CQRS/MediatR. JWT signing key must come from a secret source. OAuth code + PKCE is deferred to a future standards-based OIDC module.
