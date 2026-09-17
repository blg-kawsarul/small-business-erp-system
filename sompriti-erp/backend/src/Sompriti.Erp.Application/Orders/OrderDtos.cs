using Sompriti.Erp.Application.Common;
using Sompriti.Erp.Domain.Enums;

namespace Sompriti.Erp.Application.Orders;

public sealed record OrderListQuery : PageQuery
{
    public PostingStatus? PostingStatus { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public Guid? PartyUuid { get; init; }
    public Guid? CompanyUuid { get; init; }
    public bool DueOnly { get; init; }
}

public sealed record OrderListItemDto(
    Guid Uuid, TransactionType TransactionType, string OrderNumber, DateOnly OrderDate,
    Guid CompanyUuid, string CompanyName, Guid PartyUuid, string PartyName, string PartyCode,
    PaymentType PaymentType, PostingStatus PostingStatus, decimal TotalAmount, decimal TotalPaidAmount, decimal DueAmount,
    DateTimeOffset CreatedDate, string CreatedByUserName, Guid Revision);

public sealed record OrderPartyDto(Guid Uuid, string Name, string Code, string MobileNumber, string? Address, string? City);

public sealed record OrderCompanyDto(Guid Uuid, string CompanyName, string CompanyCode);

public sealed record OrderLineDto(
    Guid Uuid, int LineNumber, Guid ProductUuid, string ProductCode, string ProductName,
    QuantityType QuantityType, int? BoxQuantity, int? PcsQuantity, int? PcsPerBoxSnapshot, int TotalQuantityPcs,
    decimal PerPcsPrice, decimal? PerBoxPrice, decimal TotalPrice);

public sealed record OrderPaymentDto(
    Guid Uuid, DateOnly PaymentDate, decimal PaymentAmount, PaymentMethod PaymentMethod, string? PaymentNote,
    DateTimeOffset CreatedDate, string CreatedByUserName);

public sealed record OrderDetailDto(
    Guid Uuid, TransactionType TransactionType, string OrderNumber, OrderCompanyDto Company, OrderPartyDto Party,
    PaymentType PaymentType, PostingStatus PostingStatus, DateOnly OrderDate, string? Notes,
    decimal TotalAmount, decimal TotalPaidAmount, decimal DueAmount,
    DateTimeOffset? FinalizedDate, string? FinalizedByUserName,
    DateTimeOffset? VoidedDate, string? VoidedByUserName, string? VoidReason,
    Guid Revision, DateTimeOffset CreatedDate, DateTimeOffset UpdatedDate, string CreatedByUserName, string UpdatedByUserName,
    IReadOnlyList<OrderLineDto> Lines, IReadOnlyList<OrderPaymentDto> Payments);

public sealed record OrderLineRequest(
    Guid? Uuid, Guid? ProductUuid, QuantityType? QuantityType, int? BoxQuantity, int? PcsQuantity,
    int? TotalQuantityPcs, decimal? PerPcsPrice, decimal? PerBoxPrice, decimal? TotalPrice);

public sealed record OrderSaveRequest(
    Guid? CompanyUuid, Guid? PartyUuid, PaymentType? PaymentType, DateOnly? OrderDate, string? Notes,
    IReadOnlyList<OrderLineRequest>? Lines, Guid? Revision);

public sealed record VoidOrderRequest(Guid Revision, string? VoidReason);

public sealed record AddPaymentRequest(Guid Revision, DateOnly? PaymentDate, decimal? PaymentAmount, PaymentMethod? PaymentMethod, string? PaymentNote);
