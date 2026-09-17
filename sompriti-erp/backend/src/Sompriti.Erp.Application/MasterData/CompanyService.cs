using Microsoft.EntityFrameworkCore;
using Sompriti.Erp.Application.Common;
using Sompriti.Erp.Domain.Common;
using Sompriti.Erp.Domain.Entities;
using Sompriti.Erp.Domain.Enums;

namespace Sompriti.Erp.Application.MasterData;

public sealed record CompanyDto(
    Guid Uuid, string CompanyName, string CompanyCode, string? AddressLine, string? City, string? State,
    string? PostalCode, string? PhoneNumber, string? Email, string? LicenseNumber, Guid Revision,
    DateTimeOffset CreatedDate, DateTimeOffset UpdatedDate, string CreatedByUserName, string UpdatedByUserName);

public sealed record CompanySaveRequest(
    string? CompanyName, string? CompanyCode, string? AddressLine, string? City, string? State,
    string? PostalCode, string? PhoneNumber, string? Email, string? LicenseNumber, Guid? Revision);

public sealed class CompanyService(IAppDbContext db)
{
    private static readonly Dictionary<string, System.Linq.Expressions.Expression<Func<Company, object?>>> SortMap = new()
    {
        ["companyName"] = x => x.CompanyName,
        ["companyCode"] = x => x.CompanyCode,
        ["city"] = x => x.City,
        ["createdDate"] = x => x.CreatedDate,
    };

    public static CompanyDto ToDto(Company c) => new(c.Uuid, c.CompanyName, c.CompanyCode, c.AddressLine, c.City, c.State,
        c.PostalCode, c.PhoneNumber, c.Email, c.LicenseNumber, c.Revision, c.CreatedDate, c.UpdatedDate,
        c.CreatedByUserName, c.UpdatedByUserName);

    public Task<PagedResult<CompanyDto>> ListAsync(PageQuery q, CancellationToken ct)
    {
        var query = db.Companies.AsNoTracking().Where(x => x.Status == RecordStatus.Active);
        if (q.Term is { } t)
        {
            var term = t.ToLower();
            query = query.Where(x => x.CompanyName.ToLower().Contains(term) || x.CompanyCode.ToLower().Contains(term));
        }
        return query.ApplySort(q.Sort, SortMap, s => s.OrderBy(x => x.CompanyName))
            .ToPagedAsync(q, c => new CompanyDto(c.Uuid, c.CompanyName, c.CompanyCode, c.AddressLine, c.City, c.State,
                c.PostalCode, c.PhoneNumber, c.Email, c.LicenseNumber, c.Revision, c.CreatedDate, c.UpdatedDate,
                c.CreatedByUserName, c.UpdatedByUserName), ct);
    }

    public async Task<IReadOnlyList<DropdownItem>> DropdownAsync(CancellationToken ct) =>
        await db.Companies.AsNoTracking()
            .Where(x => x.Status == RecordStatus.Active)
            .OrderBy(x => x.CompanyName)
            .Select(x => new DropdownItem(x.Uuid, x.CompanyCode, x.CompanyName, x.CompanyName + " (" + x.CompanyCode + ")"))
            .ToListAsync(ct);

    public async Task<CompanyDto> GetAsync(Guid id, CancellationToken ct)
    {
        var c = await db.Companies.AsNoTracking().FirstOrDefaultAsync(x => x.Uuid == id && x.Status == RecordStatus.Active, ct);
        return ToDto(c.OrNotFound("Company"));
    }

    public async Task<CompanyDto> CreateAsync(CompanySaveRequest r, CancellationToken ct)
    {
        Validate(r);
        var code = r.CompanyCode!.Trim();
        if (await db.Companies.AnyAsync(x => x.CompanyCode == code, ct))
            throw DomainException.Conflict(ErrorCodes.DuplicateCode, $"Company code '{code}' is already used.");
        var c = new Company { Uuid = Guid.NewGuid() };
        Apply(c, r);
        db.Companies.Add(c);
        await db.SaveChangesAsync(ct);
        return ToDto(c);
    }

    public async Task<CompanyDto> UpdateAsync(Guid id, CompanySaveRequest r, CancellationToken ct)
    {
        Validate(r);
        var c = (await db.Companies.FirstOrDefaultAsync(x => x.Uuid == id && x.Status == RecordStatus.Active, ct)).OrNotFound("Company");
        RevisionGuard.Check(db, c, r.Revision ?? Guid.Empty);
        var code = r.CompanyCode!.Trim();
        if (await db.Companies.AnyAsync(x => x.CompanyCode == code && x.Uuid != id, ct))
            throw DomainException.Conflict(ErrorCodes.DuplicateCode, $"Company code '{code}' is already used.");
        Apply(c, r);
        await db.SaveChangesAsync(ct);
        return ToDto(c);
    }

    public async Task DeleteAsync(Guid id, Guid revision, CancellationToken ct)
    {
        var c = (await db.Companies.FirstOrDefaultAsync(x => x.Uuid == id && x.Status == RecordStatus.Active, ct)).OrNotFound("Company");
        RevisionGuard.Check(db, c, revision);
        var inDraft = await db.PurchaseOrders.AnyAsync(o => o.CompanyUuid == id && o.Status == RecordStatus.Active && o.PostingStatus == PostingStatus.Draft, ct)
                   || await db.SalesOrders.AnyAsync(o => o.CompanyUuid == id && o.Status == RecordStatus.Active && o.PostingStatus == PostingStatus.Draft, ct);
        if (inDraft)
            throw DomainException.Rule(ErrorCodes.InUseByDraft, "This company is used by draft orders. Finalize, void or delete those drafts first.");
        c.Status = RecordStatus.Deleted;
        await db.SaveChangesAsync(ct);
    }

    private static void Validate(CompanySaveRequest r)
    {
        var v = new Validator()
            .Required("companyName", r.CompanyName, "Company name", 150)
            .Required("companyCode", r.CompanyCode, "Company code", 20)
            .MaxLength("addressLine", r.AddressLine, "Address", 300)
            .MaxLength("city", r.City, "City", 100)
            .MaxLength("state", r.State, "State", 100)
            .MaxLength("postalCode", r.PostalCode, "Postal code", 20)
            .MaxLength("phoneNumber", r.PhoneNumber, "Phone number", 30)
            .MaxLength("email", r.Email, "Email", 200)
            .MaxLength("licenseNumber", r.LicenseNumber, "License number", 100);
        v.When(!string.IsNullOrWhiteSpace(r.Email) && !r.Email.Contains('@'), "email", "Email is not valid.");
        v.ThrowIfInvalid();
    }

    private static void Apply(Company c, CompanySaveRequest r)
    {
        c.CompanyName = r.CompanyName!.Trim();
        c.CompanyCode = r.CompanyCode!.Trim();
        c.AddressLine = Validator.Clean(r.AddressLine);
        c.City = Validator.Clean(r.City);
        c.State = Validator.Clean(r.State);
        c.PostalCode = Validator.Clean(r.PostalCode);
        c.PhoneNumber = Validator.Clean(r.PhoneNumber);
        c.Email = Validator.Clean(r.Email);
        c.LicenseNumber = Validator.Clean(r.LicenseNumber);
    }
}
