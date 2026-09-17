using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sompriti.Erp.Application.Common;
using Sompriti.Erp.Domain.Common;
using Sompriti.Erp.Domain.Entities;
using Sompriti.Erp.Domain.Enums;
using Sompriti.Erp.Domain.Rules;

namespace Sompriti.Erp.Application.Auth;

public sealed record RegisterRequest(string? UserName, string? Email, string? PhoneNumber, string? Password);
public sealed record LoginRequest(string? Email, string? Password);
public sealed record RefreshRequest(string? RefreshToken);
public sealed record ForgotPasswordRequest(string? Email);
public sealed record ResetPasswordRequest(string? Token, string? NewPassword);
public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

public sealed record CurrentUserDto(Guid Uuid, string UserName, string Email, string PhoneNumber, Role Role,
    Guid? SupplierUuid, string? SupplierName, Guid? CustomerUuid, string? CustomerName, bool MustChangePassword);

public sealed record AuthResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt, CurrentUserDto User);

public sealed class AuthOptions
{
    public const string Section = "Auth";
    public int RefreshTokenDays { get; set; } = 7;
    public int MaxFailedLogins { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    public int PasswordResetMinutes { get; set; } = 30;
}

public sealed class AuthService(
    IAppDbContext db, ICurrentUser currentUser, IPasswordHasher hasher, ITokenService tokens, IEmailSender email,
    IOptions<AuthOptions> authOptions, IOptions<AppOptions> appOptions, TimeProvider clock, ILogger<AuthService> logger)
{
    private readonly AuthOptions _opt = authOptions.Value;

    public async Task<AuthResponse> RegisterAsync(RegisterRequest r, CancellationToken ct)
    {
        var phone = ValidateProfile(r.UserName, r.Email, r.PhoneNumber, r.Password);
        var emailNorm = r.Email!.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email.ToLower() == emailNorm, ct))
            throw DomainException.Conflict(ErrorCodes.DuplicateCode, "An account with this email already exists.");

        var user = new AppUser
        {
            Uuid = Guid.NewGuid(),
            UserName = r.UserName!.Trim(),
            Email = emailNorm,
            PhoneNumber = phone,
            PasswordHash = hasher.Hash(r.Password!),
            Role = Role.User, // SRS 5: registration always creates USER
        };
        db.SetAuditUser(user.Uuid, user.UserName);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return await IssueAsync(user, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Email) || string.IsNullOrEmpty(r.Password))
            throw new DomainException(ErrorKind.Unauthorized, ErrorCodes.InvalidCredentials, "Invalid email or password.");

        var emailNorm = r.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == emailNorm && u.Status == RecordStatus.Active, ct);
        var now = clock.GetUtcNow();

        if (user is null)
        {
            hasher.Hash(r.Password); // similar timing for unknown accounts
            throw new DomainException(ErrorKind.Unauthorized, ErrorCodes.InvalidCredentials, "Invalid email or password.");
        }

        db.SetAuditUser(user.Uuid, user.UserName);

        if (user.LockoutEndDate is { } until && until > now)
        {
            var minutes = Math.Max(1, (int)Math.Ceiling((until - now).TotalMinutes));
            throw new DomainException(ErrorKind.Unauthorized, ErrorCodes.AccountLocked,
                $"Too many failed attempts. Try again in {minutes} minute(s).");
        }

        if (!hasher.Verify(user.PasswordHash, r.Password))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= _opt.MaxFailedLogins)
            {
                user.FailedLoginCount = 0;
                user.LockoutEndDate = now.AddMinutes(_opt.LockoutMinutes);
            }
            await db.SaveChangesAsync(ct);
            throw new DomainException(ErrorKind.Unauthorized, ErrorCodes.InvalidCredentials, "Invalid email or password.");
        }

        user.FailedLoginCount = 0;
        user.LockoutEndDate = null;
        user.LastLoginDate = now;
        await db.SaveChangesAsync(ct);
        return await IssueAsync(user, ct);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.RefreshToken))
            throw new DomainException(ErrorKind.Unauthorized, ErrorCodes.InvalidToken, "Session expired. Please log in again.");
        var hash = HashToken(r.RefreshToken);
        var now = clock.GetUtcNow();
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null || token.RevokedDate is not null || token.ExpiresDate <= now)
            throw new DomainException(ErrorKind.Unauthorized, ErrorCodes.InvalidToken, "Session expired. Please log in again.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Uuid == token.UserUuid && u.Status == RecordStatus.Active, ct);
        if (user is null)
            throw new DomainException(ErrorKind.Unauthorized, ErrorCodes.InvalidToken, "Session expired. Please log in again.");

        token.RevokedDate = now; // rotation
        return await IssueAsync(user, ct);
    }

    public async Task LogoutAsync(RefreshRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.RefreshToken)) return;
        var hash = HashToken(r.RefreshToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash && t.RevokedDate == null, ct);
        if (token is null) return;
        token.RevokedDate = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    public async Task<CurrentUserDto> MeAsync(CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Uuid == currentUser.UserUuid && u.Status == RecordStatus.Active, ct);
        return await ToDtoAsync(user.OrNotFound("User"), ct);
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest r, CancellationToken ct)
    {
        var v = new Validator();
        v.When(string.IsNullOrEmpty(r.CurrentPassword), "currentPassword", "Current password is required.");
        v.When(!PasswordPolicy.IsValid(r.NewPassword), "newPassword", PasswordPolicy.Description);
        v.ThrowIfInvalid();

        var user = (await db.Users.FirstOrDefaultAsync(u => u.Uuid == currentUser.UserUuid && u.Status == RecordStatus.Active, ct)).OrNotFound("User");
        if (!hasher.Verify(user.PasswordHash, r.CurrentPassword!))
            throw DomainException.Validation("currentPassword", "Current password is incorrect.");
        if (hasher.Verify(user.PasswordHash, r.NewPassword!))
            throw DomainException.Validation("newPassword", "New password must be different from the current password.");

        user.PasswordHash = hasher.Hash(r.NewPassword!);
        user.MustChangePassword = false;
        await RevokeAllAsync(user.Uuid, ct);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>SRS 6.8: always succeeds from the caller's point of view.</summary>
    public async Task ForgotPasswordAsync(ForgotPasswordRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Email)) return;
        var emailNorm = r.Email.Trim().ToLowerInvariant();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email.ToLower() == emailNorm && u.Status == RecordStatus.Active, ct);
        if (user is null) return;

        var now = clock.GetUtcNow();
        var raw = NewToken();
        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            Uuid = Guid.NewGuid(),
            UserUuid = user.Uuid,
            TokenHash = HashToken(raw),
            CreatedDate = now,
            ExpiresDate = now.AddMinutes(_opt.PasswordResetMinutes),
        });
        await db.SaveChangesAsync(ct);

        var link = $"{appOptions.Value.PublicBaseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(raw)}";
        var name = WebUtility.HtmlEncode(user.UserName);
        var app = WebUtility.HtmlEncode(appOptions.Value.BusinessName);
        var html = $"""
            <p>Hello {name},</p>
            <p>We received a request to reset your {app} password.</p>
            <p><a href="{link}">Reset your password</a></p>
            <p>This link expires in {_opt.PasswordResetMinutes} minutes. If you did not request this, you can ignore this email.</p>
            """;
        try
        {
            await email.SendAsync(user.Email, user.UserName, $"{appOptions.Value.BusinessName}: reset your password", html, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Password reset email failed for user {UserUuid}", user.Uuid);
        }
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Token))
            throw DomainException.Rule(ErrorCodes.InvalidToken, "This reset link is invalid or has expired.");
        if (!PasswordPolicy.IsValid(r.NewPassword))
            throw DomainException.Validation("newPassword", PasswordPolicy.Description);

        var now = clock.GetUtcNow();
        var hash = HashToken(r.Token);
        var token = await db.PasswordResetTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null || token.UsedDate is not null || token.ExpiresDate <= now)
            throw DomainException.Rule(ErrorCodes.InvalidToken, "This reset link is invalid or has expired.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Uuid == token.UserUuid && u.Status == RecordStatus.Active, ct);
        if (user is null)
            throw DomainException.Rule(ErrorCodes.InvalidToken, "This reset link is invalid or has expired.");

        db.SetAuditUser(user.Uuid, user.UserName);
        token.UsedDate = now;
        user.PasswordHash = hasher.Hash(r.NewPassword!);
        user.MustChangePassword = false;
        user.FailedLoginCount = 0;
        user.LockoutEndDate = null;
        await RevokeAllAsync(user.Uuid, ct);
        await db.SaveChangesAsync(ct);

        try
        {
            await email.SendAsync(user.Email, user.UserName, $"{appOptions.Value.BusinessName}: your password was changed",
                $"<p>Hello {WebUtility.HtmlEncode(user.UserName)},</p><p>Your password was changed. If this was not you, contact your administrator immediately.</p>", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Password changed confirmation email failed for user {UserUuid}", user.Uuid);
        }
    }

    // ------------------------------------------------------------------ helpers

    public async Task RevokeAllAsync(Guid userUuid, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var active = await db.RefreshTokens.Where(t => t.UserUuid == userUuid && t.RevokedDate == null).ToListAsync(ct);
        foreach (var t in active) t.RevokedDate = now;
    }

    private async Task<AuthResponse> IssueAsync(AppUser user, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var (access, accessExp) = tokens.CreateAccessToken(user);
        var raw = NewToken();
        var refresh = new RefreshToken
        {
            Uuid = Guid.NewGuid(),
            UserUuid = user.Uuid,
            TokenHash = HashToken(raw),
            CreatedDate = now,
            ExpiresDate = now.AddDays(_opt.RefreshTokenDays),
        };
        db.RefreshTokens.Add(refresh);
        await db.SaveChangesAsync(ct);
        return new AuthResponse(access, accessExp, raw, refresh.ExpiresDate, await ToDtoAsync(user, ct));
    }

    private async Task<CurrentUserDto> ToDtoAsync(AppUser u, CancellationToken ct)
    {
        var supplierName = u.SupplierUuid is { } sid
            ? await db.Suppliers.AsNoTracking().Where(s => s.Uuid == sid).Select(s => s.Name).FirstOrDefaultAsync(ct) : null;
        var customerName = u.CustomerUuid is { } cid
            ? await db.Customers.AsNoTracking().Where(s => s.Uuid == cid).Select(s => s.Name).FirstOrDefaultAsync(ct) : null;
        return new CurrentUserDto(u.Uuid, u.UserName, u.Email, BdMobile.ToDisplay(u.PhoneNumber), u.Role,
            u.SupplierUuid, supplierName, u.CustomerUuid, customerName, u.MustChangePassword);
    }

    /// <summary>Validates name/email/phone/password; returns the normalized phone.</summary>
    internal static string ValidateProfile(string? userName, string? emailAddress, string? phone, string? password, bool passwordRequired = true)
    {
        var v = new Validator()
            .Required("userName", userName, "Name", 100)
            .Required("email", emailAddress, "Email", 200)
            .Required("phoneNumber", phone, "Phone number");
        v.When(!string.IsNullOrWhiteSpace(emailAddress) && !IsEmail(emailAddress), "email", "Email is not valid.");
        var normalized = BdMobile.Normalize(phone);
        v.When(!string.IsNullOrWhiteSpace(phone) && normalized is null, "phoneNumber", "Enter a valid Bangladesh mobile number, e.g. 01712345678.");
        if (passwordRequired || !string.IsNullOrEmpty(password))
            v.When(!PasswordPolicy.IsValid(password), "password", PasswordPolicy.Description);
        v.ThrowIfInvalid();
        return normalized!;
    }

    private static bool IsEmail(string value)
    {
        var s = value.Trim();
        var at = s.IndexOf('@');
        return at > 0 && at == s.LastIndexOf('@') && s.IndexOf('.', at) > at + 1 && !s.EndsWith('.') && !s.Contains(' ');
    }

    private static string NewToken() => Base64Url(RandomNumberGenerator.GetBytes(48));

    public static string HashToken(string raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
