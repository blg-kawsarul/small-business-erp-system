using Microsoft.EntityFrameworkCore;
using Sompriti.Erp.Application.Auth;
using Sompriti.Erp.Application.Common;
using Sompriti.Erp.Domain.Common;
using Sompriti.Erp.Domain.Entities;
using Sompriti.Erp.Domain.Enums;
using Sompriti.Erp.Domain.Rules;

namespace Sompriti.Erp.Application.Users;

public sealed record UserDto(Guid Uuid, string UserName, string Email, string PhoneNumber, Role Role,
    Guid? SupplierUuid, string? SupplierName, string? SupplierCode, Guid? CustomerUuid, string? CustomerName, string? CustomerCode,
    RecordStatus Status, DateTimeOffset? LastLoginDate, bool IsLocked, Guid Revision, DateTimeOffset CreatedDate, string CreatedByUserName);

/// <summary>Password is required on create; optional on update (sets a new password when provided).</summary>
public sealed record UserSaveRequest(string? UserName, string? Email, string? PhoneNumber, Role? Role,
    Guid? SupplierUuid, Guid? CustomerUuid, string? Password, Guid? Revision);

public sealed record UserListQuery : PageQuery
{
    public Role? Role { get; init; }
    public bool IncludeDeleted { get; init; }
}

public sealed class UserService(IAppDbContext db, ICurrentUser currentUser, IPasswordHasher hasher, AuthService auth, TimeProvider clock)
{
    public async Task<PagedResult<UserDto>> ListAsync(UserListQuery q, CancellationToken ct)
    {
        var users = db.Users.AsNoTracking().Where(u => u.Uuid != AppUser.SystemUserUuid);
        if (!q.IncludeDeleted) users = users.Where(u => u.Status == RecordStatus.Active);
        if (q.Role is { } role) users = users.Where(u => u.Role == role);
        if (q.Term is { } t)
        {
            var term = t.ToLower();
            users = users.Where(u => u.UserName.ToLower().Contains(term) || u.Email.ToLower().Contains(term) || u.PhoneNumber.Contains(term));
        }
        var page = await Project(users.OrderBy(u => u.UserName)).ToPagedAsync(q, x => x, ct);
        return page with { Items = page.Items.Select(Display).ToList() };
    }

    public async Task<UserDto> GetAsync(Guid id, CancellationToken ct)
    {
        var dto = await Project(db.Users.AsNoTracking().Where(u => u.Uuid == id && u.Uuid != AppUser.SystemUserUuid)).FirstOrDefaultAsync(ct);
        return Display(dto.OrNotFound("User"));
    }

    private IQueryable<UserDto> Project(IQueryable<AppUser> users)
    {
        var now = clock.GetUtcNow();
        return from u in users
               join s in db.Suppliers.AsNoTracking() on u.SupplierUuid equals (Guid?)s.Uuid into sj
               from s in sj.DefaultIfEmpty()
               join c in db.Customers.AsNoTracking() on u.CustomerUuid equals (Guid?)c.Uuid into cj
               from c in cj.DefaultIfEmpty()
               select new UserDto(u.Uuid, u.UserName, u.Email, u.PhoneNumber, u.Role,
                   u.SupplierUuid, s == null ? null : s.Name, s == null ? null : s.Code,
                   u.CustomerUuid, c == null ? null : c.Name, c == null ? null : c.Code,
                   u.Status, u.LastLoginDate, u.LockoutEndDate != null && u.LockoutEndDate > now,
                   u.Revision, u.CreatedDate, u.CreatedByUserName);
    }

    private static UserDto Display(UserDto u) => u with { PhoneNumber = BdMobile.ToDisplay(u.PhoneNumber) };

    public async Task<UserDto> CreateAsync(UserSaveRequest r, CancellationToken ct)
    {
        var phone = AuthService.ValidateProfile(r.UserName, r.Email, r.PhoneNumber, r.Password);
        if (r.Role is null) throw DomainException.Validation("role", "Role is required.");
        var emailNorm = r.Email!.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email.ToLower() == emailNorm, ct))
            throw DomainException.Conflict(ErrorCodes.DuplicateCode, "An account with this email already exists.");
        await ValidateLinksAsync(r, ct);

        var user = new AppUser
        {
            Uuid = Guid.NewGuid(),
            UserName = r.UserName!.Trim(),
            Email = emailNorm,
            PhoneNumber = phone,
            Role = r.Role.Value,
            SupplierUuid = r.SupplierUuid,
            CustomerUuid = r.CustomerUuid,
            PasswordHash = hasher.Hash(r.Password!),
            MustChangePassword = true,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return await GetAsync(user.Uuid, ct);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UserSaveRequest r, CancellationToken ct)
    {
        var phone = AuthService.ValidateProfile(r.UserName, r.Email, r.PhoneNumber, r.Password, passwordRequired: false);
        if (r.Role is null) throw DomainException.Validation("role", "Role is required.");
        var user = (await db.Users.FirstOrDefaultAsync(u => u.Uuid == id && u.Status == RecordStatus.Active && u.Uuid != AppUser.SystemUserUuid, ct))
            .OrNotFound("User");
        RevisionGuard.Check(db, user, r.Revision ?? Guid.Empty);

        var emailNorm = r.Email!.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email.ToLower() == emailNorm && u.Uuid != id, ct))
            throw DomainException.Conflict(ErrorCodes.DuplicateCode, "An account with this email already exists.");
        await ValidateLinksAsync(r, ct);

        if (user.Role != r.Role)
        {
            if (id == currentUser.UserUuid)
                throw DomainException.Rule(ErrorCodes.BusinessRule, "You cannot change your own role.");
            if (user.Role == Role.Admin) await EnsureAnotherAdminAsync(id, ct);
        }

        user.UserName = r.UserName!.Trim();
        user.Email = emailNorm;
        user.PhoneNumber = phone;
        user.Role = r.Role.Value;
        user.SupplierUuid = r.SupplierUuid;
        user.CustomerUuid = r.CustomerUuid;
        if (!string.IsNullOrEmpty(r.Password))
        {
            user.PasswordHash = hasher.Hash(r.Password);
            user.MustChangePassword = id != currentUser.UserUuid;
            user.FailedLoginCount = 0;
            user.LockoutEndDate = null;
            await auth.RevokeAllAsync(id, ct);
        }
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task UnlockAsync(Guid id, Guid revision, CancellationToken ct)
    {
        var user = (await db.Users.FirstOrDefaultAsync(u => u.Uuid == id && u.Status == RecordStatus.Active, ct)).OrNotFound("User");
        RevisionGuard.Check(db, user, revision);
        user.FailedLoginCount = 0;
        user.LockoutEndDate = null;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, Guid revision, CancellationToken ct)
    {
        var user = (await db.Users.FirstOrDefaultAsync(u => u.Uuid == id && u.Status == RecordStatus.Active && u.Uuid != AppUser.SystemUserUuid, ct))
            .OrNotFound("User");
        RevisionGuard.Check(db, user, revision);
        if (id == currentUser.UserUuid)
            throw DomainException.Rule(ErrorCodes.BusinessRule, "You cannot delete your own account.");
        if (user.Role == Role.Admin) await EnsureAnotherAdminAsync(id, ct);
        user.Status = RecordStatus.Deleted;
        await auth.RevokeAllAsync(id, ct); // SRS 6.6: deleted users lose their sessions
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureAnotherAdminAsync(Guid exceptId, CancellationToken ct)
    {
        var others = await db.Users.CountAsync(u => u.Role == Role.Admin && u.Status == RecordStatus.Active && u.Uuid != exceptId, ct);
        if (others == 0)
            throw DomainException.Rule(ErrorCodes.BusinessRule, "At least one active ADMIN must remain.");
    }

    private async Task ValidateLinksAsync(UserSaveRequest r, CancellationToken ct)
    {
        if (r.SupplierUuid is { } sid && !await db.Suppliers.AnyAsync(s => s.Uuid == sid && s.Status == RecordStatus.Active, ct))
            throw DomainException.Validation("supplierUuid", "Selected supplier is not available.");
        if (r.CustomerUuid is { } cid && !await db.Customers.AnyAsync(c => c.Uuid == cid && c.Status == RecordStatus.Active, ct))
            throw DomainException.Validation("customerUuid", "Selected buyer is not available.");
    }
}
