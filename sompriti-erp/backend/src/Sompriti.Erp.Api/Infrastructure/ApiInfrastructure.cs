using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Sompriti.Erp.Application.Common;
using Sompriti.Erp.Domain.Common;
using Sompriti.Erp.Domain.Enums;
using Sompriti.Erp.Infrastructure.Persistence;
using Sompriti.Erp.Infrastructure.Security;

namespace Sompriti.Erp.Api.Infrastructure;

public static class Roles
{
    public const string Admin = "ADMIN";
    public const string Manager = "MANAGER";
    public const string User = "USER";
    public const string AdminOrManager = "ADMIN,MANAGER";
}

public static class ErpClaims
{
    public const string UserUuid = "sub";
    public const string Name = "name";
    public const string Role = "role";
    public const string SupplierUuid = "supplier_uuid";
    public const string CustomerUuid = "customer_uuid";
    public const string MustChangePassword = "must_change_password";
}

/// <summary>Reads the current user from the claims created by <see cref="BearerAuthenticationHandler"/>.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
    public Guid UserUuid => Guid.TryParse(Principal?.FindFirstValue(ErpClaims.UserUuid), out var id) ? id : Guid.Empty;
    public string UserName => Principal?.FindFirstValue(ErpClaims.Name) ?? "";
    public Role Role => EnumText.TryParse<Role>(Principal?.FindFirstValue(ErpClaims.Role), out var r) ? r : Role.User;
    public Guid? SupplierUuid => Guid.TryParse(Principal?.FindFirstValue(ErpClaims.SupplierUuid), out var id) ? id : null;
    public Guid? CustomerUuid => Guid.TryParse(Principal?.FindFirstValue(ErpClaims.CustomerUuid), out var id) ? id : null;
}

/// <summary>
/// Validates the bearer access token, then loads the user from the database so that role changes,
/// supplier/buyer links and deletion take effect immediately (SRS 6.6).
/// </summary>
public sealed class BearerAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
    JwtTokenService tokens, AppDbContext db)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Bearer";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var claims = tokens.Validate(header["Bearer ".Length..].Trim());
        if (claims is null) return AuthenticateResult.Fail("Invalid or expired token.");

        var user = await db.Users.AsNoTracking()
            .Where(u => u.Uuid == claims.UserUuid && u.Status == RecordStatus.Active)
            .Select(u => new { u.Uuid, u.UserName, u.Role, u.SupplierUuid, u.CustomerUuid, u.MustChangePassword })
            .FirstOrDefaultAsync(Context.RequestAborted);
        if (user is null) return AuthenticateResult.Fail("User is not active.");

        var list = new List<Claim>
        {
            new(ErpClaims.UserUuid, user.Uuid.ToString()),
            new(ErpClaims.Name, user.UserName),
            new(ErpClaims.Role, EnumText.ToText(user.Role)),
            new(ErpClaims.MustChangePassword, user.MustChangePassword ? "true" : "false"),
        };
        if (user.SupplierUuid is { } s) list.Add(new Claim(ErpClaims.SupplierUuid, s.ToString()));
        if (user.CustomerUuid is { } c) list.Add(new Claim(ErpClaims.CustomerUuid, c.ToString()));

        var identity = new ClaimsIdentity(list, SchemeName, ErpClaims.Name, ErpClaims.Role);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        await Response.WriteAsJsonAsync(Problem(401, "UNAUTHORIZED", "Please log in to continue."));
    }

    protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        await Response.WriteAsJsonAsync(Problem(403, ErrorCodes.Forbidden, "You do not have permission to perform this action."));
    }

    private static ProblemDetails Problem(int status, string code, string detail) => new()
    {
        Status = status,
        Title = detail,
        Detail = detail,
        Extensions = { ["code"] = code },
    };
}

/// <summary>Blocks everything except profile/password endpoints while the user must change their password.</summary>
public sealed class PasswordChangeRequiredMiddleware(RequestDelegate next)
{
    private static readonly string[] Allowed =
    [
        "/api/v1/auth/me", "/api/v1/auth/change-password", "/api/v1/auth/logout", "/api/v1/auth/refresh", "/api/v1/auth/login"
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && context.User.FindFirstValue(ErpClaims.MustChangePassword) == "true"
            && context.Request.Path.StartsWithSegments("/api")
            && !Allowed.Any(a => context.Request.Path.Equals(a, StringComparison.OrdinalIgnoreCase)))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = 403,
                Title = "You must change your password before continuing.",
                Detail = "You must change your password before continuing.",
                Extensions = { ["code"] = "PASSWORD_CHANGE_REQUIRED" },
            });
            return;
        }
        await next(context);
    }
}

/// <summary>Maps exceptions to RFC 7807 problem details (SRS 13.3).</summary>
public sealed class ErpExceptionHandler(ILogger<ErpExceptionHandler> logger, IHostEnvironment env) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, code, message, errors, details) = Map(exception);
        if (status >= 500) logger.LogError(exception, "Unhandled error on {Method} {Path}", context.Request.Method, context.Request.Path);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = message,
            Detail = status >= 500 && !env.IsDevelopment() ? "An unexpected error occurred. Please try again." : message,
            Instance = context.Request.Path,
        };
        problem.Extensions["code"] = code;
        if (errors is { Count: > 0 }) problem.Extensions["errors"] = errors;
        if (details is not null) problem.Extensions["details"] = details;
        if (status >= 500 && !env.IsDevelopment()) problem.Title = "Unexpected error";

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(problem, ErpJson.Default, "application/problem+json", ct);
        return true;
    }

    private static (int, string, string, IReadOnlyDictionary<string, string[]>?, object?) Map(Exception ex)
    {
        switch (ex)
        {
            case DomainException d:
                var status = d.Kind switch
                {
                    ErrorKind.Validation => 400,
                    ErrorKind.Unauthorized => 401,
                    ErrorKind.Forbidden => 403,
                    ErrorKind.NotFound => 404,
                    ErrorKind.Conflict => 409,
                    _ => 422
                };
                return (status, d.Code, d.Message, d.Errors, d.Details);
            case DbUpdateConcurrencyException:
                return (409, ErrorCodes.RevisionConflict, "This record was changed by someone else. Please reload and try again.", null, null);
            case DbUpdateException { InnerException: PostgresException pg }:
                return MapPostgres(pg);
            case PostgresException pg:
                return MapPostgres(pg);
            case BadHttpRequestException or JsonException:
                return (400, ErrorCodes.Validation, "The request body is not valid.", null, null);
            case OperationCanceledException:
                return (499, "REQUEST_CANCELLED", "The request was cancelled.", null, null);
            default:
                return (500, "INTERNAL_ERROR", ex.Message, null, null);
        }
    }

    private static (int, string, string, IReadOnlyDictionary<string, string[]>?, object?) MapPostgres(PostgresException pg) => pg.SqlState switch
    {
        PostgresErrorCodes.UniqueViolation => (409, ErrorCodes.DuplicateCode, "A record with the same code or number already exists.", null, null),
        PostgresErrorCodes.ForeignKeyViolation => (422, ErrorCodes.BusinessRule, "The record references data that does not exist or is still in use.", null, null),
        PostgresErrorCodes.CheckViolation when pg.ConstraintName?.Contains("stock_balance") == true
            => (422, ErrorCodes.InsufficientStock, "Stock cannot become negative.", null, null),
        PostgresErrorCodes.CheckViolation => (400, ErrorCodes.Validation, "One or more values are out of the allowed range.", null, null),
        PostgresErrorCodes.LockNotAvailable or PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure
            => (409, ErrorCodes.RevisionConflict, "The record is busy. Please try again.", null, null),
        _ => (500, "DATABASE_ERROR", pg.MessageText, null, null)
    };
}

/// <summary>Serializes enums as UPPER_SNAKE_CASE strings and accepts either form when reading.</summary>
public sealed class EnumTextJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(Converter<>).MakeGenericType(typeToConvert))!;

    private sealed class Converter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
    {
        public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String && EnumText.TryParse<TEnum>(reader.GetString(), out var value))
                return value;
            throw new JsonException($"Invalid value for {typeof(TEnum).Name}.");
        }

        public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
            writer.WriteStringValue(EnumText.ToText(value.ToString()));
    }
}

public static class ErpJson
{
    public static readonly JsonSerializerOptions Default = Configure(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    public static JsonSerializerOptions Configure(JsonSerializerOptions o)
    {
        o.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        o.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        if (!o.Converters.OfType<EnumTextJsonConverterFactory>().Any()) o.Converters.Add(new EnumTextJsonConverterFactory());
        return o;
    }
}

/// <summary>Small helpers shared by controllers.</summary>
public static class ControllerHelpers
{
    public static FileContentResult PdfFile(this ControllerBase c, byte[] bytes, string fileName, bool download)
    {
        if (!download)
        {
            c.Response.Headers.ContentDisposition = $"inline; filename=\"{fileName}\"";
            return c.File(bytes, "application/pdf");
        }
        return c.File(bytes, "application/pdf", fileName);
    }
}
