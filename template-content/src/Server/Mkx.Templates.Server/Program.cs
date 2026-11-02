using Microsoft.AspNetCore.Authentication;
using System.Text;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Mkx.Templates.Application.Abstractions;
using Mkx.Templates.Application.Services;
using Mkx.Templates.Domain.Identity;
using Mkx.Templates.Infrastructure.Persistence;
using Mkx.Templates.Server.Auth;
using Mkx.Templates.Server.Hosting;
using Mkx.Templates.Server.OpenApi;
using Swashbuckle.AspNetCore.SwaggerUI;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(ApiDocumentation.Configure);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();
builder.Services.AddOptions<OpaqueIntrospectionOptions>()
    .Bind(builder.Configuration.GetSection(OpaqueIntrospectionOptions.Section))
    .Validate(options => options.IsValid(),
        "OpaqueIntrospection requires HTTPS Endpoint and Issuer, API Audience, ClientId, and ClientSecret.")
    .ValidateOnStart();
builder.Services.AddHttpClient(OpaqueIntrospectionHandler.HttpClientName, client =>
{
    client.Timeout = TimeSpan.FromSeconds(6);
    client.MaxResponseContentBufferSize = 16 * 1024;
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AllowAutoRedirect = false
});
builder.Services.AddSingleton<IAccessTokenIssuer, JwtTokenIssuer>();

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.Section))
    .ValidateDataAnnotations()
    .Validate(o => Encoding.UTF8.GetByteCount(o.SigningKey) >= 32, "Jwt:SigningKey must have at least 32 UTF-8 bytes")
    .ValidateOnStart();

var connection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlServer(connection, sql => sql.EnableRetryOnFailure()));
builder.Services.AddIdentityCore<AppUser>(options =>
{
    options.User.RequireUniqueEmail = false;
    options.Password.RequiredLength = 12;
    options.Password.RequiredUniqueChars = 4;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.SignIn.RequireConfirmedAccount = true;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
}).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<AppDbContext>()
  .AddSignInManager().AddDefaultTokenProviders();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SigningKey"] ?? "")),
            ValidateIssuer = true, ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true, ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = ClaimTypes.Name, RoleClaimType = ClaimTypes.Role
        };
    })
    .AddJwtBearer("ExternalOidc", options =>
    {
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = true;
        // Resolve from IConfiguration during options construction (works with test overrides too).
        var externalEnabled = builder.Configuration.GetValue<bool>("ExternalOidc:Enabled");
        var externalAuthority = builder.Configuration["ExternalOidc:Authority"];
        var externalAudience = builder.Configuration["ExternalOidc:Audience"];
        if (externalEnabled)
        {
            if (!Uri.TryCreate(externalAuthority, UriKind.Absolute, out var authorityUri) ||
                authorityUri.Scheme != Uri.UriSchemeHttps ||
                string.IsNullOrWhiteSpace(externalAudience))
                throw new InvalidOperationException("Enabled ExternalOidc requires an HTTPS Authority and non-empty Audience.");
            // Microsoft JWT middleware resolves signing keys from the IdP discovery/JWKS over HTTPS.
            options.Authority = externalAuthority;
            options.Audience = externalAudience;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = "sub",
                RoleClaimType = "roles"
            };
        }
        else
        {
            // Register a fail-closed scheme so the external endpoint returns 401 when disabled.
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    context.NoResult();
                    return Task.CompletedTask;
                }
            };
        }
    });
// Force validation of external JWT options at startup, before the first HTTP request.
builder.Services.AddOptions<JwtBearerOptions>("ExternalOidc").ValidateOnStart();
builder.Services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, OpaqueIntrospectionHandler>(
    OpaqueIntrospectionHandler.SchemeName, _ => { });
builder.Services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
    ApiKeyAuthenticationHandler.SchemeName, _ => { });
builder.Services.AddAuthorization(options =>
{
    // New controllers are protected by default unless explicitly marked [AllowAnonymous].
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser().Build();
    options.AddPolicy("AdminOnly", policy => policy
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
        .RequireRole("Administrator"));
    options.AddPolicy("IntegrationRead", policy => policy
        .AddAuthenticationSchemes(ApiKeyAuthenticationHandler.SchemeName)
        .RequireAuthenticatedUser()
        .RequireClaim("scope", "integration:read"));
    options.AddPolicy("IntegrationWrite", policy => policy
        .AddAuthenticationSchemes(ApiKeyAuthenticationHandler.SchemeName)
        .RequireAuthenticatedUser()
        .RequireClaim("scope", "integration:write"));    options.AddPolicy("ExternalOpaqueOnly", policy => policy
        .AddAuthenticationSchemes(OpaqueIntrospectionHandler.SchemeName)
        .RequireAuthenticatedUser());
    options.AddPolicy("ExternalOidcOnly", policy => policy
        .AddAuthenticationSchemes("ExternalOidc")
        .RequireAuthenticatedUser());
});

var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (origins.Length > 0)
    builder.Services.AddCors(o => o.AddPolicy("Clients", p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

var authPermitLimit = Math.Clamp(builder.Configuration.GetValue("RateLimit:AuthRequestsPerMinute", 10), 1, 1000);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = authPermitLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
        }));
});
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

var app = builder.Build();
if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}
await BootstrapAdminSeeder.SeedAsync(app.Services, app.Configuration);

app.UseExceptionHandler();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseStaticFiles();
    app.UseSwaggerUI(options =>
    {
        options.DocumentTitle = "Mkx API | Developer Portal";
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Mkx API v1");
        options.InjectStylesheet("/swagger-ui/custom.css");
        options.EnableDeepLinking();
        options.EnableFilter();
        options.DisplayOperationId();
        options.DisplayRequestDuration();
        options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
        options.DefaultModelsExpandDepth(-1);
    });
}
app.UseRateLimiter();
if (origins.Length > 0) app.UseCors("Clients");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health/ready").AllowAnonymous();
app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" }))
    .WithName("Health_Live").WithTags("06 Health").WithSummary("API process liveness")
    .WithDescription("Returns 200 when the API process is responding. No database access.")
    .AllowAnonymous();
app.Run();

public partial class Program { }
