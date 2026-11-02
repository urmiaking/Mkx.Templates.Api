# Mkx.Templates.Api template repository
The distributable generated solution is in template-content. Read template-content/AGENTS.md, docs/ROADMAP.md and docs/PHASE_LOG.md before changing it.
Preserve the pragmatic Mkx.Templates.Blazor backend layers; do not add CQRS/MediatR, UI dependencies, or credentials.
Run build, test project (from template-content), dotnet new package instantiation, and phase documentation for changes. Use scripts/smoke-sql.ps1 for optional real SQL Server checks.

Before changing HTTP routes read template-content/docs/ai/OPENAPI.md and .matemcp/skills/openapi-contract/SKILL.md. Preserve grouped Swagger UI, stable operation IDs, real security requirements and OpenApiContractTests.
