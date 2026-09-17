using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sompriti.Erp.Api.Infrastructure;
using Sompriti.Erp.Application.Common;
using Sompriti.Erp.Application.Reports;
using Sompriti.Erp.Application.Sms;
using Sompriti.Erp.Application.Stock;

namespace Sompriti.Erp.Api.Controllers;

[ApiController]
[Route("api/v1/stock")]
[Authorize] // roles are set per action
public sealed class StockController(StockService service) : ControllerBase
{
    [HttpGet("balances"), Authorize(Roles = Roles.AdminOrManager)]
    public Task<PagedResult<StockBalanceDto>> Balances([FromQuery] PageQuery q, [FromQuery] Guid? productUuid,
        [FromQuery] bool lowStockOnly, CancellationToken ct) => service.BalancesAsync(q, productUuid, lowStockOnly, ct);

    /// <summary>MANAGER sees sales movements only (filtered in the service).</summary>
    [HttpGet("ledger"), Authorize(Roles = Roles.AdminOrManager)]
    public Task<PagedResult<StockLedgerDto>> Ledger([FromQuery] StockLedgerQuery q, CancellationToken ct) => service.LedgerAsync(q, ct);

    [HttpGet("adjustments"), Authorize(Roles = Roles.Admin)]
    public Task<PagedResult<StockAdjustmentDto>> Adjustments([FromQuery] PageQuery q, [FromQuery] Guid? productUuid, CancellationToken ct) =>
        service.AdjustmentsAsync(q, productUuid, ct);

    [HttpPost("adjustments"), Authorize(Roles = Roles.Admin)]
    public Task<StockAdjustmentDto> CreateAdjustment(StockAdjustmentRequest r, CancellationToken ct) => service.CreateAdjustmentAsync(r, ct);
}

[ApiController]
[Route("api/v1/reports")]
[Authorize] // role and linked-entity scoping is enforced in ReportService
public sealed class ReportsController(ReportService service) : ControllerBase
{
    [HttpGet("customers")]
    public Task<PartyReport> Customers([FromQuery] ReportQuery q, CancellationToken ct) => service.CustomerReportAsync(q, ct);

    [HttpGet("suppliers"), Authorize(Roles = "ADMIN,USER")]
    public Task<PartyReport> Suppliers([FromQuery] ReportQuery q, CancellationToken ct) => service.SupplierReportAsync(q, ct);

    [HttpGet("companies"), Authorize(Roles = Roles.AdminOrManager)]
    public Task<IReadOnlyList<CompanyReportRow>> Companies([FromQuery] ReportQuery q, CancellationToken ct) => service.CompanyReportAsync(q, ct);
}

[ApiController]
[Route("api/v1/dashboard")]
[Authorize]
public sealed class DashboardController(ReportService service) : ControllerBase
{
    [HttpGet]
    public Task<DashboardDto> Get([FromQuery] Guid? companyUuid, CancellationToken ct) => service.DashboardAsync(companyUuid, ct);
}

[ApiController]
[Route("api/v1/sms")]
[Authorize(Roles = Roles.Admin)]
public sealed class SmsController(SmsService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<SmsLogDto>> List([FromQuery] SmsLogQuery q, CancellationToken ct) => service.ListAsync(q, ct);

    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct)
    {
        await service.RetryAsync(id, ct);
        return NoContent();
    }
}
