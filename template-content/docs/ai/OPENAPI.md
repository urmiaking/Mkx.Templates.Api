# API documentation contract: Swagger & OpenAPI

The API-only template ships an interactive Swagger developer portal at **/swagger** and a machine-readable OpenAPI 3.x document at **/swagger/v1/swagger.json** in **Development only**. Neither endpoint nor its styles are exposed in Production by default. The portal is intentionally self-hosted and uses no external styles, scripts, fonts or image CDNs.

## Architectural ownership

All OpenAPI configuration lives in `src/Server/Mkx.Templates.Server/OpenApi/ApiDocumentation.cs`. The portal is styled in `src/Server/Mkx.Templates.Server/wwwroot/swagger-ui/custom.css`. These files are **part of the template**; always update them in `template-content`, never in a throwaway generated project.

## Required conventions for every new endpoint

1. Choose an existing functional group or add a **new stable, ordered group** in `ApiDocumentation.Groups`. Never leave an endpoint in `99 Other`; keep groups oriented around business capabilities, not HTTP verbs.
2. Assign an explicit, descriptive **operation summary**, and document its behavior, side effects, major validation rules and unexpected errors. No generic `Controller: Method` summaries on published API operations.
3. Preserve a **stable, unique operationId** in the form `Controller_Action`; changing one breaks generated SDKs. When renaming a controller/action intentionally, treat that as a contract change.
4. Declare accurate **request and response DTOs**, HTTP status codes and validation boundaries. Put public contracts in `Shared`, not directly in controller methods.
5. Preserve the **least privileged security scheme** on the operation:
   - `LocalJwt`: local Identity JWT (including `AdminOnly`).
   - `ExternalOidc`: external provider JWT access token, no local role mapping.
   - `OpaqueToken`: opaque access token verified via HTTPS introspection.
   - `ApiKey`: `X-API-Key` for service-to-service access, explicit scoped policy.
   - Truly public routes: explicit `[AllowAnonymous]` / `.AllowAnonymous()`.
6. Do not document authorization headers as ordinary string parameters: **security definitions and operation requirements** describe them. Never embed passwords, tokens, secret values, private service addresses or production hosts into examples.
7. Add/extend tests in `Tests/Mkx.Templates.Tests/OpenApiContractTests.cs` covering the new tags, operationId, security scheme, HTTP status codes and exposure boundaries.
8. After changes, verify source **build + tests**, packaged `dotnet new mkx-api` **build + tests**, and run the SQL smoke test for auth/persistence changes. Update `docs/ROADMAP.md` and `docs/PHASE_LOG.md`.

## Security and API stability

The OpenAPI contract must reflect **actual** server authorization. A local Administrator bearer token must never become a valid ApiKey or external token; no external IdP role automatically maps into the local `Administrator` policy. Introspection must check active, issuer, audience, expiry and subject. Swagger UI authorization dialogs may contain credentials entered by the developer, so the portal is disabled outside Development. Do not enable `Try it out` in a public deployment without an explicit threat model.

This template intentionally provides an API resource server, not an OpenIddict authorization server. Native applications should use a trusted IdP's **Authorization Code + PKCE** if federation is required. Do not create homemade OAuth authorization flows in the template.

## Visual design rules

Keep the portal cohesive: dark slate surfaces, high-contrast text, mint accents, unobtrusive HTTP-method markers, soft rounded section panels, responsive layout, and no heavy dependencies. Preserve keyboard controls and visible method/response semantics. If refactoring the stylesheet, verify the actual served CSS and HTML, not just file existence.
