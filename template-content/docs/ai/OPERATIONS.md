# Setup and operations

1. Supply ConnectionStrings__DefaultConnection for SQL Server; use a restricted account in production.
2. Supply Jwt__SigningKey with a random secret of at least 32 UTF-8 bytes. Configure Jwt__Issuer and Jwt__Audience for the environment; never commit secrets.
3. Create/review initial migration with:
   `dotnet ef migrations add InitialIdentity --project src/Core/Mkx.Templates.Infrastructure --startup-project src/Server/Mkx.Templates.Server --output-dir Persistence/Migrations`
   Apply with `dotnet ef database update --project src/Core/Mkx.Templates.Infrastructure --startup-project src/Server/Mkx.Templates.Server` only against the intended database. Automatic migrations disabled by default.
4. Optional one-time admin: configure BootstrapAdmin__Enabled=true, BootstrapAdmin__Username and BootstrapAdmin__Password securely. After successful startup, disable the flag and remove password from environment. Existing accounts are never overwritten or promoted.
5. Development-only Swagger /swagger, health endpoints /health/live (process) and /health/ready (database).
6. Add explicit Cors__AllowedOrigins__0 etc for browser clients. Native mobile apps do not require browser CORS.
7. Store mobile tokens in platform secure storage. Local login is appropriate only for trusted first-party clients over TLS; for general third-party mobile clients implement OAuth authorization code + PKCE as a separate optional phase.
8. In production configure TLS termination, proxy trust, secret rotation and audit/monitoring appropriate to deployment.

Known limits: no self-service registration, password recovery, SMS/OTP, passkeys or OIDC provider in this first version. Logout invalidates refresh tokens but already-issued access JWTs remain valid until expiry.
