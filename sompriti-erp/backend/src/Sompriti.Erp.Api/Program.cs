using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Sompriti.Erp.Api.Infrastructure;
using Sompriti.Erp.Application;
using Sompriti.Erp.Application.Common;
using Sompriti.Erp.Infrastructure;
using Sompriti.Erp.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Railway provides PORT; listen on all interfaces.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port)) builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services
    .AddAuthentication(BearerAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, BearerAuthenticationHandler>(BearerAuthenticationHandler.SchemeName, _ => { });
builder.Services.AddAuthorization();

builder.Services.AddControllers()
    .AddJsonOptions(o => ErpJson.Configure(o.JsonSerializerOptions))
    .ConfigureApiBehaviorOptions(o =>
    {
        // Return model-binding errors in the same problem+json shape as business errors.
        o.InvalidModelStateResponseFactory = ctx =>
        {
            var errors = ctx.ModelState.Where(kv => kv.Value?.Errors.Count > 0)
                .ToDictionary(kv => string.IsNullOrEmpty(kv.Key) ? "body" : char.ToLowerInvariant(kv.Key[0]) + kv.Key[1..],
                    kv => kv.Value!.Errors.Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "Invalid value." : e.ErrorMessage).ToArray());
            var problem = new ValidationProblemDetails(errors)
            {
                Status = 400,
                Title = "The request is not valid.",
                Detail = errors.Values.SelectMany(v => v).FirstOrDefault() ?? "The request is not valid.",
            };
            problem.Extensions["code"] = "VALIDATION_ERROR";
            return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
        };
    });
builder.Services.ConfigureHttpJsonOptions(o => ErpJson.Configure(o.SerializerOptions));

builder.Services.AddExceptionHandler<ErpExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy("refresh", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    // Railway terminates TLS at its proxy.
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var corsOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
if (corsOrigins.Length > 0)
    builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.XContentTypeOptions = "nosniff";
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    ctx.Response.Headers.XFrameOptions = "DENY";
    await next();
});

// Fail fast when required secrets are missing (throws if Jwt:SigningKey is not configured).
app.Services.GetRequiredService<Sompriti.Erp.Infrastructure.Security.JwtTokenService>();

// Apply SQL migrations and seed the first admin before accepting traffic.
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(CancellationToken.None);
}

if (corsOrigins.Length > 0) app.UseCors();

// Serve the Angular build (wwwroot) with long caching for hashed assets.
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var name = ctx.File.Name;
        ctx.Context.Response.Headers.CacheControl = name == "index.html" ? "no-cache" : "public,max-age=31536000,immutable";
    }
});

app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<PasswordChangeRequiredMiddleware>();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

// Unknown /api routes return 404 JSON; everything else falls back to the Angular app.
app.Map("/api/{**rest}", (HttpContext ctx) => Results.Problem(statusCode: 404, title: "Not found",
    extensions: new Dictionary<string, object?> { ["code"] = "NOT_FOUND" }));
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
