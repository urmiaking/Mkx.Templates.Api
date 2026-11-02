# Mkx API OpenAPI contract guardrail

When changing endpoints in Mkx.Templates.Api, **read `template-content/docs/ai/OPENAPI.md`** and `template-content/AGENTS.md` first.

Documentation source: `template-content/src/Server/Mkx.Templates.Server/OpenApi/ApiDocumentation.cs`.
Portal stylesheet: `template-content/src/Server/Mkx.Templates.Server/wwwroot/swagger-ui/custom.css`.

Every new endpoint requires a stable Controller_Action operation ID, meaningful summary, categorized tag (never 99 Other), accurate response DTO/status metadata, and explicit policy-matching OpenAPI security. Do not mix LocalJwt, ExternalOidc, OpaqueToken, or ApiKey; public routes need explicit AllowAnonymous. Update the OpenAPI contract tests, source and generated-template tests, ROADMAP and PHASE_LOG. Production Swagger stays disabled. Do not document unimplemented server features or include credentials in examples.
