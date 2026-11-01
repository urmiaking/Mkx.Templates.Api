# ADR 0001 — Retain Blazor backend's pragmatic layers

Date: 2026-11-02. Status: Accepted.

Decision: use Domain, Infrastructure, Shared, Application, Server, tests with MVC controllers and service contracts. EF configurations and Identity stores live in Infrastructure, business use cases in Application, and HTTP/token signing at Server. Application intentionally references Infrastructure in keeping with Mkx.Templates.Blazor. Reject extra mediator/CQRS/repository wrappers without a concrete requirement.

Motivation: familiar structure, low conceptual and build overhead, reusable backend for multiple clients.

JWT Bearer is the first native-app authentication mode; opaque rotated refresh tokens are stored hashed in SQL Server. Do not invent an OAuth authorization endpoint or call this an OIDC provider. OpenIddict or an external provider will be a separate optional extension.
