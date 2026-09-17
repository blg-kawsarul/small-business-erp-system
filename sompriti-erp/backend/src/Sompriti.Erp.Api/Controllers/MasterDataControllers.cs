using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sompriti.Erp.Api.Infrastructure;
using Sompriti.Erp.Application.Common;
using Sompriti.Erp.Application.MasterData;
using Sompriti.Erp.Application.Users;

namespace Sompriti.Erp.Api.Controllers;

[ApiController]
[Route("api/v1/companies")]
[Authorize] // roles are set per action
public sealed class CompaniesController(CompanyService service) : ControllerBase
{
    [HttpGet, Authorize(Roles = Roles.Admin)]
    public Task<PagedResult<CompanyDto>> List([FromQuery] PageQuery q, CancellationToken ct) => service.ListAsync(q, ct);

    /// <summary>Order screens need the company dropdown, so MANAGER may read it.</summary>
    [HttpGet("dropdown"), Authorize(Roles = Roles.AdminOrManager)]
    public Task<IReadOnlyList<DropdownItem>> Dropdown(CancellationToken ct) => service.DropdownAsync(ct);

    [HttpGet("{id:guid}"), Authorize(Roles = Roles.Admin)]
    public Task<CompanyDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPost, Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<CompanyDto>> Create(CompanySaveRequest r, CancellationToken ct)
    {
        var dto = await service.CreateAsync(r, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Uuid }, dto);
    }

    [HttpPut("{id:guid}"), Authorize(Roles = Roles.Admin)]
    public Task<CompanyDto> Update(Guid id, CompanySaveRequest r, CancellationToken ct) => service.UpdateAsync(id, r, ct);

    [HttpDelete("{id:guid}"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid revision, CancellationToken ct)
    {
        await service.DeleteAsync(id, revision, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/customers")]
[Authorize(Roles = Roles.AdminOrManager)]
public sealed class CustomersController(CustomerService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<PartyDto>> List([FromQuery] PageQuery q, CancellationToken ct) => service.ListAsync(q, ct);

    [HttpGet("dropdown")]
    public Task<IReadOnlyList<DropdownItem>> Dropdown([FromQuery] string? search, CancellationToken ct) => service.DropdownAsync(search, ct);

    [HttpGet("{id:guid}")]
    public Task<PartyDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<PartyDto>> Create(PartySaveRequest r, CancellationToken ct)
    {
        var dto = await service.CreateAsync(r, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Uuid }, dto);
    }

    [HttpPut("{id:guid}")]
    public Task<PartyDto> Update(Guid id, PartySaveRequest r, CancellationToken ct) => service.UpdateAsync(id, r, ct);

    /// <summary>SRS 5.1: MANAGER can create and edit customers; delete is ADMIN only.</summary>
    [HttpDelete("{id:guid}"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid revision, CancellationToken ct)
    {
        await service.DeleteAsync(id, revision, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/suppliers")]
[Authorize(Roles = Roles.Admin)]
public sealed class SuppliersController(SupplierService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<PartyDto>> List([FromQuery] PageQuery q, CancellationToken ct) => service.ListAsync(q, ct);

    [HttpGet("dropdown")]
    public Task<IReadOnlyList<DropdownItem>> Dropdown([FromQuery] string? search, CancellationToken ct) => service.DropdownAsync(search, ct);

    [HttpGet("{id:guid}")]
    public Task<PartyDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<PartyDto>> Create(PartySaveRequest r, CancellationToken ct)
    {
        var dto = await service.CreateAsync(r, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Uuid }, dto);
    }

    [HttpPut("{id:guid}")]
    public Task<PartyDto> Update(Guid id, PartySaveRequest r, CancellationToken ct) => service.UpdateAsync(id, r, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid revision, CancellationToken ct)
    {
        await service.DeleteAsync(id, revision, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/products")]
[Authorize(Roles = Roles.AdminOrManager)]
public sealed class ProductsController(ProductService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ProductDto>> List([FromQuery] PageQuery q, [FromQuery] bool lowStockOnly, CancellationToken ct) =>
        service.ListAsync(q, lowStockOnly, ct);

    [HttpGet("dropdown")]
    public Task<IReadOnlyList<ProductDropdownItem>> Dropdown([FromQuery] string? search, CancellationToken ct) => service.DropdownAsync(search, ct);

    [HttpGet("{id:guid}")]
    public Task<ProductDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPost, Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ProductDto>> Create(ProductSaveRequest r, CancellationToken ct)
    {
        var dto = await service.CreateAsync(r, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Uuid }, dto);
    }

    [HttpPut("{id:guid}"), Authorize(Roles = Roles.Admin)]
    public Task<ProductDto> Update(Guid id, ProductSaveRequest r, CancellationToken ct) => service.UpdateAsync(id, r, ct);

    [HttpDelete("{id:guid}"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid revision, CancellationToken ct)
    {
        await service.DeleteAsync(id, revision, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/users")]
[Authorize(Roles = Roles.Admin)]
public sealed class UsersController(UserService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<UserDto>> List([FromQuery] UserListQuery q, CancellationToken ct) => service.ListAsync(q, ct);

    [HttpGet("{id:guid}")]
    public Task<UserDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(UserSaveRequest r, CancellationToken ct)
    {
        var dto = await service.CreateAsync(r, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Uuid }, dto);
    }

    [HttpPut("{id:guid}")]
    public Task<UserDto> Update(Guid id, UserSaveRequest r, CancellationToken ct) => service.UpdateAsync(id, r, ct);

    [HttpPost("{id:guid}/unlock")]
    public async Task<IActionResult> Unlock(Guid id, RevisionRequest r, CancellationToken ct)
    {
        await service.UnlockAsync(id, r.Revision, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid revision, CancellationToken ct)
    {
        await service.DeleteAsync(id, revision, ct);
        return NoContent();
    }
}
