# Mkx API-only application

Run these commands from the generated solution directory:

```powershell
dotnet build Mkx.Templates.slnx
dotnet test --project Tests/Mkx.Templates.Tests/Mkx.Templates.Tests.csproj
dotnet run --project src/Server/Mkx.Templates.Server
```

Read `AGENTS.md`, `docs/ai/ARCHITECTURE.md` and `docs/ai/OPERATIONS.md`. Configure a unique JWT key through environment variables or user-secrets. The included initial EF migration must be applied before database-backed requests.
