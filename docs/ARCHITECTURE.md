# Mkx.Templates.Api architecture

The reference implementation is `Mkx.Templates.Blazor/template-content` (inspection on 2026-11-02), especially `docs/ai/ARCHITECTURE.md`, `AUTHENTICATION.md`, `DATABASE.md`, and `SERVICE_ARCHITECTURE.md`.

| Layer | Responsibilities | Dependencies |
|---|---|---|
| Domain | `AppUser`, refresh session domain state | ASP.NET Identity model primitives, not EF |
| Infrastructure | SQL Server EF Core context, Identity stores, persistence configuration | Domain |
| Shared | REST DTOs and stable route constants | none |
| Application | `IAuthService`, use-case orchestration, Identity management, refresh token rotation | Infrastructure, Shared |
| Server | MVC controllers, host/middleware, JWT validation/issuance, health, bootstrap | Application |
| Tests | Real ASP.NET HTTP pipeline smoke checks | Server |

This deliberately matches the **pragmatic layered** backend architecture of Mkx.Templates.Blazor, including Application -> Infrastructure dependency rather than introducing CQRS or a strict Clean Architecture rewrite.

The API host never references UI assemblies. Identity uses username/password and confirmed accounts; JWT access tokens are intended for API calls from React Native, Flutter, React and other API consumers. The server issues locally signed access tokens and non-JWT opaque refresh tokens. Access tokens are short-lived; refresh tokens are random, stored only as SHA-256 hashes, rotated using an atomic EF update, scoped to a session family, and invalidated on suspected replay. The JWT signing secret is injected outside source control.

**Security limitation:** JWT revocation is not instantaneous for already-issued access tokens; they expire after their short lifetime. Revoking sessions stops future refresh. There is no external IdP, authorization-code flow, client registration, user sign-up, passkeys or provider-managed OTP in this baseline.

**Host defaults:** HTTP-only service, no UI assets, CORS deny-by-default, throttled auth endpoints, MVC validation and ProblemDetails, SQL Server and /health/live vs /health/ready.

**Intentional divergence from Blazor:** cookie/SSR account adapters and client-side shared services are not copied because they are UI-specific. Policies are enforced in API middleware/controllers.
