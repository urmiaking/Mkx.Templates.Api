# Mkx.Templates.Api — Generated application agent guide

This is a backend-only .NET 10 solution. Read `docs/ai/ARCHITECTURE.md` and `docs/ai/OPERATIONS.md` before changes.

- Preserve the pragmatic Mkx.Templates.Blazor backend boundaries: Domain (entities and invariants), Infrastructure (EF Core and Identity stores), Shared (DTOs/routes), Application (use cases), Server (HTTP/JWT/hosting).
- No Blazor, SSR, WASM, CQRS, MediatR, forced repository/unit-of-work wrappers or client dependencies.
- Controllers delegate to Application. Do not put business behavior or DbContext directly in controllers.
- Enforce permissions on the server; client-side visibility is never authorization.
- JWT access tokens are short-lived and signed; opaque refresh tokens are hashed, rotated atomically and revocable.
- No hardcoded credentials, signing keys, production DB strings or generated bin/obj files in Git.
- Run `dotnet build Mkx.Templates.slnx` and `dotnet test --project Tests/Mkx.Templates.Tests/Mkx.Templates.Tests.csproj` from the solution directory. Keep docs and tests aligned with behavior.
