using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Sompriti.Erp.Application.Common;
using Sompriti.Erp.Domain.Common;
using Sompriti.Erp.Domain.Entities;

namespace Sompriti.Erp.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string Section = "Jwt";
    /// <summary>At least 32 characters. Set JWT__SigningKey in production.</summary>
    public string SigningKey { get; set; } = "";
    public string Issuer { get; set; } = "sompriti-erp";
    public string Audience { get; set; } = "sompriti-erp";
    public int AccessTokenMinutes { get; set; } = 15;
}

/// <summary>Claims carried in the access token. Role and links are re-read from the database on every request.</summary>
public sealed record AccessTokenClaims(Guid UserUuid, string UserName, DateTimeOffset ExpiresAt);

/// <summary>Minimal HS256 JSON Web Token implementation (sign + validate) using only the .NET base library.</summary>
public sealed class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly byte[] _key;
    private readonly TimeProvider _clock;

    public JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock)
    {
        _options = options.Value;
        _clock = clock;
        if (string.IsNullOrWhiteSpace(_options.SigningKey) || _options.SigningKey.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey (env JWT__SigningKey) must be set and at least 32 characters long.");
        _key = Encoding.UTF8.GetBytes(_options.SigningKey);
    }

    private sealed class Payload
    {
        [JsonPropertyName("sub")] public string Sub { get; set; } = "";
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("role")] public string Role { get; set; } = "";
        [JsonPropertyName("iss")] public string Iss { get; set; } = "";
        [JsonPropertyName("aud")] public string Aud { get; set; } = "";
        [JsonPropertyName("iat")] public long Iat { get; set; }
        [JsonPropertyName("exp")] public long Exp { get; set; }
        [JsonPropertyName("jti")] public string Jti { get; set; } = "";
    }

    private static readonly string HeaderSegment = B64Url(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));

    public (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(AppUser user)
    {
        var now = _clock.GetUtcNow();
        var exp = now.AddMinutes(_options.AccessTokenMinutes);
        var payload = new Payload
        {
            Sub = user.Uuid.ToString(),
            Name = user.UserName,
            Role = EnumText.ToText(user.Role),
            Iss = _options.Issuer,
            Aud = _options.Audience,
            Iat = now.ToUnixTimeSeconds(),
            Exp = exp.ToUnixTimeSeconds(),
            Jti = Guid.NewGuid().ToString("N"),
        };
        var body = B64Url(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signingInput = HeaderSegment + "." + body;
        var sig = B64Url(HMACSHA256.HashData(_key, Encoding.ASCII.GetBytes(signingInput)));
        return (signingInput + "." + sig, exp);
    }

    public AccessTokenClaims? Validate(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3 || parts[0] != HeaderSegment) return null;

        var expected = HMACSHA256.HashData(_key, Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]));
        byte[] actual;
        try { actual = FromB64Url(parts[2]); } catch (FormatException) { return null; }
        if (!CryptographicOperations.FixedTimeEquals(expected, actual)) return null;

        Payload? payload;
        try { payload = JsonSerializer.Deserialize<Payload>(FromB64Url(parts[1])); }
        catch (Exception ex) when (ex is JsonException or FormatException) { return null; }
        if (payload is null || payload.Iss != _options.Issuer || payload.Aud != _options.Audience) return null;

        var expires = DateTimeOffset.FromUnixTimeSeconds(payload.Exp);
        if (expires <= _clock.GetUtcNow()) return null;
        if (!Guid.TryParse(payload.Sub, out var userUuid)) return null;
        return new AccessTokenClaims(userUuid, payload.Name, expires);
    }

    private static string B64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromB64Url(string s)
    {
        var b = s.Replace('-', '+').Replace('_', '/');
        b += (b.Length % 4) switch { 2 => "==", 3 => "=", 0 => "", _ => throw new FormatException() };
        return Convert.FromBase64String(b);
    }
}

/// <summary>Uses ASP.NET Core Identity's PBKDF2 password hasher.</summary>
public sealed class IdentityPasswordHasher : Application.Common.IPasswordHasher
{
    private readonly PasswordHasher<AppUser> _inner = new();
    private static readonly AppUser Dummy = new();

    public string Hash(string password) => _inner.HashPassword(Dummy, password);

    public bool Verify(string hash, string password)
    {
        try
        {
            return _inner.VerifyHashedPassword(Dummy, hash, password) != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
