using Microsoft.OpenApi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.AspNetCore.RateLimiting;

namespace Mkx.Templates.Server.OpenApi;

/// <summary>One place for stable OpenAPI grouping, security, and operation metadata.</summary>
public static class ApiDocumentation
{
    public const string DocumentName = "v1";
    private static readonly IReadOnlyDictionary<string, string> Groups = new Dictionary<string, string>
    {
        ["Auth"] = "01 Authentication",
        ["Onboarding"] = "01 Authentication",
        ["Authenticator"] = "01 Authentication",
        ["Passkeys"] = "01 Authentication",
        ["ExternalIdentity"] = "02 External identity",
        ["ExternalLinks"] = "02 External identity",
        ["OpaqueIdentity"] = "02 External identity",
        ["Users"] = "03 Users & access",
        ["ApiKeys"] = "04 API key management",
        ["Integrations"] = "05 Service integrations"
    };

    public static void Configure(SwaggerGenOptions options)
    {
        options.SwaggerDoc(DocumentName, new OpenApiInfo
        {
            Title = "Mkx • API",
            Version = DocumentName,
            Description = "Backend API reference. Local JWT, external OAuth JWT and service API keys are separate schemes. " +
                          "Use the security scheme shown on each operation. API documentation is development-only.",
            Contact = new OpenApiContact { Name = "Mkx Templates" }
        });
        options.TagActionsBy(api => api.ActionDescriptor is ControllerActionDescriptor
            ? [Groups.GetValueOrDefault(
            api.ActionDescriptor.RouteValues.TryGetValue("controller", out var name) ? name ?? "" : "") ?? "99 Other"]
            : ["06 Health"]);
        options.OrderActionsBy(api => api.RelativePath + "_" + api.HttpMethod);
        options.CustomOperationIds(api =>
        {
            var descriptor = api.ActionDescriptor as ControllerActionDescriptor;
            return descriptor is not null ? $"{descriptor.ControllerName}_{descriptor.ActionName}" :
                api.ActionDescriptor.EndpointMetadata.OfType<Microsoft.AspNetCore.Routing.IEndpointNameMetadata>()
                    .FirstOrDefault()?.EndpointName;
        });

        options.AddSecurityDefinition("LocalJwt", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
            Description = "Local ASP.NET Identity JWT from POST /api/auth/login."
        });
        options.AddSecurityDefinition("ExternalOidc", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
            Description = "External IdP JWT ACCESS token for ExternalOidc:Audience, not an ID token."
        });
        options.AddSecurityDefinition("OpaqueToken", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "opaque",
            Description = "External opaque access token, validated over HTTPS via OAuth introspection."
        });
        options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Header,
            Name = "X-API-Key", Description = "Administrator-issued service key (never put in client apps)."
        });
        options.OperationFilter<SecurityAndDocumentationFilter>();
    }
}

public sealed class SecurityAndDocumentationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var descriptor = context.ApiDescription.ActionDescriptor as ControllerActionDescriptor;
        var controller = descriptor?.ControllerName ?? "";
        var action = descriptor?.ActionName ?? context.ApiDescription.HttpMethod ?? "Operation";
        operation.Summary ??= (controller, action) switch
        {
            ("Auth", "Login") => "Sign in with a username and password",
            ("Auth", "Refresh") => "Rotate a refresh token",
            ("Auth", "Logout") => "Sign out from the current session",
            ("Auth", "LogoutAll") => "Revoke all refresh sessions",
            ("Auth", "Me") => "Get the current local account",
            ("Passkeys", "RegisterOptions") => "Create short-lived passkey attestation options for a verified local user",
            ("Passkeys", "RegisterComplete") => "Verify WebAuthn attestation and register a passkey to its owner",
            ("Passkeys", "LoginOptions") => "Issue anonymous discoverable-passkey assertion options",
            ("Passkeys", "LoginComplete") => "Verify a WebAuthn assertion and issue local JWT credentials",
            ("Passkeys", "List") => "List the current local account registered passkeys",
            ("Passkeys", "Remove") => "Remove a passkey after local password and MFA verification",
            ("Authenticator", "Status") => "Inspect local authenticator enrollment status",
            ("Authenticator", "Setup") => "Start TOTP enrollment after password reauthentication",
            ("Authenticator", "Enable") => "Activate authenticator MFA using a valid code",
            ("Authenticator", "Disable") => "Disable MFA with password and authenticator or recovery code",
            ("Onboarding", "Register") => "Request account registration and email confirmation",
            ("Onboarding", "Confirm") => "Confirm a pending account using an emailed code",
            ("Onboarding", "Resend") => "Resend email confirmation when the account is pending",
            ("Onboarding", "ForgotPassword") => "Request a password reset code by username and email",
            ("Onboarding", "ResetPassword") => "Reset a confirmed account password using an emailed code",
            ("ExternalIdentity", "Me") => "Inspect a validated external identity",
            ("ExternalLinks", "Challenge") => "Create one-time local account ownership proof",
            ("ExternalLinks", "Complete") => "Link verified external user to local account",
            ("ExternalLinks", "Me") => "Resolve verified external identity to a linked local profile",
            ("ExternalLinks", "Unlink") => "Remove your external identity link",
            ("OpaqueIdentity", "Me") => "Inspect a validated opaque OAuth access token",
            ("ApiKeys", "Create") => "Create a scoped API key (secret returned once)",
            ("ApiKeys", "Revoke") => "Revoke an API key immediately",
            ("ApiKeys", "List") => "List API key metadata, never secrets",
            ("Users", "List") => "List accounts (administrator)",
            ("Integrations", "Ping") => "Verify service API-key credentials and scopes",
            _ => $"{controller}: {action}"
        };
        operation.Description ??= controller switch
        {
            "Auth" => "Local Identity credentials and session management.",
            "Passkeys" => "Opt-in. WebAuthn challenges are SQL-backed, short-lived and one-use; RP ID and browser Origin must match configured values. Registration/removal requires local JWT and proof of account control; login uses cryptographic assertion. Additional MFA still required for MFA accounts.",
            "Authenticator" => "Local JWT only. Setup requires password; activation requires TOTP and returns recovery codes once. Never log the setup secret or codes. Enabled accounts require password plus a second factor for login.",
            "Onboarding" => "Opt-in; disabled by default. Username remains the sign-in identifier. Confirmation/recovery codes are emailed, not returned by API. Public requests are throttled and generic 202 responses avoid account enumeration.",
            "ExternalIdentity" => "Requires a validated external OAuth access token. No local administrator privileges.",
            "ExternalLinks" => "Account linking requires proof of local and external account ownership. No automatic role mapping.",
            "OpaqueIdentity" => "Requires introspection of an external opaque access token. No local account mapping.",
            "ApiKeys" => "Local Administrator only. Key material is returned once on creation.",
            "Integrations" => "Requires a service API key with integration:read scope.",
            "Users" => "A local Administrator JWT is required.",
            _ => "See the declared authorization policy."
        };

        var controllerAttributes = descriptor?.ControllerTypeInfo.GetCustomAttributes(true) ?? [];
        var actionAttributes = descriptor?.MethodInfo.GetCustomAttributes(true) ?? [];
        var metadata = controllerAttributes.Concat(actionAttributes)
            .Concat(context.ApiDescription.ActionDescriptor.EndpointMetadata).ToArray();
        if (metadata.OfType<EnableRateLimitingAttribute>().Any())
        {
            operation.Responses ??= [];
            operation.Responses.TryAdd("429", new OpenApiResponse
            {
                Description = "Rate limit exceeded. Retry after the window resets."
            });
        }

        if (metadata.OfType<IAllowAnonymous>().Any())
            return;

        var policies = metadata.OfType<IAuthorizeData>().Select(x => x.Policy).ToHashSet();
        var scheme = policies.Contains("ExternalOidcOnly") ? "ExternalOidc" :
            policies.Contains("ExternalOpaqueOnly") ? "OpaqueToken" :
            policies.Contains("IntegrationRead") || policies.Contains("IntegrationWrite") ? "ApiKey" : "LocalJwt";

        operation.Responses ??= [];
        operation.Responses.TryAdd("401", new OpenApiResponse
        {
            Description = "Missing, expired or invalid credentials."
        });
        operation.Responses.TryAdd("403", new OpenApiResponse
        {
            Description = "Authenticated but not permitted by the endpoint policy."
        });
        operation.Security ??= new List<OpenApiSecurityRequirement>();
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(scheme, context.Document)] = []
        });
    }
}
