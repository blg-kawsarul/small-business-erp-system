using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sompriti.Erp.Api.Infrastructure;
using Sompriti.Erp.Application.Common;
using Sompriti.Erp.Application.Orders;

namespace Sompriti.Erp.Api.Controllers;

/// <summary>
/// Purchase orders. Write access: ADMIN. Read access: ADMIN, and USER accounts linked to a supplier
/// (their own orders only, enforced in the service).
/// </summary>
[ApiController]
[Route("api/v1/purchase-orders")]
[Authorize] // roles are set per action: action-level role lists cannot widen a class-level list
public sealed class PurchaseOrdersController(PurchaseOrderService service) : ControllerBase
{
    [HttpGet, Authorize(Roles = "ADMIN,USER")]
    public Task<PagedResult<OrderListItemDto>> List([FromQuery] OrderListQuery q, CancellationToken ct) => service.ListAsync(q, ct);

    [HttpGet("{id:guid}"), Authorize(Roles = "ADMIN,USER")]
    public Task<OrderDetailDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpGet("{id:guid}/pdf"), Authorize(Roles = "ADMIN,USER")]
    public async Task<IActionResult> Pdf(Guid id, [FromQuery] bool download, CancellationToken ct)
    {
        var (bytes, name) = await service.PdfAsync(id, ct);
        return this.PdfFile(bytes, name, download);
    }

    [HttpPost, Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<OrderDetailDto>> Create(OrderSaveRequest r, CancellationToken ct)
    {
        var dto = await service.CreateAsync(r, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Uuid }, dto);
    }

    [HttpPut("{id:guid}"), Authorize(Roles = Roles.Admin)]
    public Task<OrderDetailDto> Update(Guid id, OrderSaveRequest r, CancellationToken ct) => service.UpdateAsync(id, r, ct);

    [HttpDelete("{id:guid}"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid revision, CancellationToken ct)
    {
        await service.DeleteAsync(id, revision, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/lines/{lineId:guid}"), Authorize(Roles = Roles.Admin)]
    public Task<OrderDetailDto> DeleteLine(Guid id, Guid lineId, [FromQuery] Guid revision, CancellationToken ct) =>
        service.DeleteLineAsync(id, lineId, revision, ct);

    [HttpPost("{id:guid}/finalize"), Authorize(Roles = Roles.Admin)]
    public Task<OrderDetailDto> Finalize(Guid id, RevisionRequest r, CancellationToken ct) => service.FinalizeAsync(id, r.Revision, ct);

    [HttpPost("{id:guid}/void"), Authorize(Roles = Roles.Admin)]
    public Task<OrderDetailDto> Void(Guid id, VoidOrderRequest r, CancellationToken ct) => service.VoidAsync(id, r, ct);

    [HttpPost("{id:guid}/payments"), Authorize(Roles = Roles.Admin)]
    public Task<OrderDetailDto> AddPayment(Guid id, AddPaymentRequest r, CancellationToken ct) => service.AddPaymentAsync(id, r, ct);

    [HttpDelete("{id:guid}/payments/{paymentId:guid}"), Authorize(Roles = Roles.Admin)]
    public Task<OrderDetailDto> DeletePayment(Guid id, Guid paymentId, [FromQuery] Guid revision, CancellationToken ct) =>
        service.DeletePaymentAsync(id, paymentId, revision, ct);
}

/// <summary>
/// Sales orders. Write access: ADMIN and MANAGER (void: ADMIN only). Read access additionally for
/// USER accounts linked to a buyer (their own orders only).
/// </summary>
[ApiController]
[Route("api/v1/sales-orders")]
[Authorize] // roles are set per action
public sealed class SalesOrdersController(SalesOrderService service) : ControllerBase
{
    [HttpGet, Authorize(Roles = "ADMIN,MANAGER,USER")]
    public Task<PagedResult<OrderListItemDto>> List([FromQuery] OrderListQuery q, CancellationToken ct) => service.ListAsync(q, ct);

    [HttpGet("{id:guid}"), Authorize(Roles = "ADMIN,MANAGER,USER")]
    public Task<OrderDetailDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpGet("{id:guid}/pdf"), Authorize(Roles = "ADMIN,MANAGER,USER")]
    public async Task<IActionResult> Pdf(Guid id, [FromQuery] bool download, CancellationToken ct)
    {
        var (bytes, name) = await service.PdfAsync(id, ct);
        return this.PdfFile(bytes, name, download);
    }

    [HttpPost, Authorize(Roles = Roles.AdminOrManager)]
    public async Task<ActionResult<OrderDetailDto>> Create(OrderSaveRequest r, CancellationToken ct)
    {
        var dto = await service.CreateAsync(r, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Uuid }, dto);
    }

    [HttpPut("{id:guid}"), Authorize(Roles = Roles.AdminOrManager)]
    public Task<OrderDetailDto> Update(Guid id, OrderSaveRequest r, CancellationToken ct) => service.UpdateAsync(id, r, ct);

    [HttpDelete("{id:guid}"), Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid revision, CancellationToken ct)
    {
        await service.DeleteAsync(id, revision, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/lines/{lineId:guid}"), Authorize(Roles = Roles.AdminOrManager)]
    public Task<OrderDetailDto> DeleteLine(Guid id, Guid lineId, [FromQuery] Guid revision, CancellationToken ct) =>
        service.DeleteLineAsync(id, lineId, revision, ct);

    [HttpPost("{id:guid}/finalize"), Authorize(Roles = Roles.AdminOrManager)]
    public Task<OrderDetailDto> Finalize(Guid id, RevisionRequest r, CancellationToken ct) => service.FinalizeAsync(id, r.Revision, ct);

    [HttpPost("{id:guid}/void"), Authorize(Roles = Roles.Admin)]
    public Task<OrderDetailDto> Void(Guid id, VoidOrderRequest r, CancellationToken ct) => service.VoidAsync(id, r, ct);

    [HttpPost("{id:guid}/payments"), Authorize(Roles = Roles.AdminOrManager)]
    public Task<OrderDetailDto> AddPayment(Guid id, AddPaymentRequest r, CancellationToken ct) => service.AddPaymentAsync(id, r, ct);

    [HttpDelete("{id:guid}/payments/{paymentId:guid}"), Authorize(Roles = Roles.AdminOrManager)]
    public Task<OrderDetailDto> DeletePayment(Guid id, Guid paymentId, [FromQuery] Guid revision, CancellationToken ct) =>
        service.DeletePaymentAsync(id, paymentId, revision, ct);
}
