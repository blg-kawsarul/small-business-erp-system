using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Sompriti.Erp.Domain.Entities;
using Sompriti.Erp.Domain.Enums;

namespace Sompriti.Erp.Application.Common;

/// <summary>The authenticated user for the current request. Role and links are loaded from the database on every request.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid UserUuid { get; }
    string UserName { get; }
    Role Role { get; }
    Guid? SupplierUuid { get; }
    Guid? CustomerUuid { get; }
}

public interface IAppDbContext
{
    DbSet<AppUser> Users { get; }
    DbSet<Company> Companies { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<Product> Products { get; }
    DbSet<StockBalance> StockBalances { get; }
    DbSet<StockLedger> StockLedgers { get; }
    DbSet<StockAdjustment> StockAdjustments { get; }
    DbSet<PurchaseOrder> PurchaseOrders { get; }
    DbSet<PurchaseOrderLineItem> PurchaseOrderLines { get; }
    DbSet<PurchaseOrderPayment> PurchaseOrderPayments { get; }
    DbSet<SalesOrder> SalesOrders { get; }
    DbSet<SalesOrderLineItem> SalesOrderLines { get; }
    DbSet<SalesOrderPayment> SalesOrderPayments { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<PasswordResetToken> PasswordResetTokens { get; }
    DbSet<SmsOutbox> SmsOutbox { get; }

    DbSet<TEntity> Set<TEntity>() where TEntity : class;
    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Tells the context which revision the client last saw, so the UPDATE is guarded by
    /// "WHERE revision = submitted" (optimistic concurrency).
    /// </summary>
    void SetOriginalRevision(AuditedEntity entity, Guid revision);

    /// <summary>Audit user to use when no authenticated user exists (e.g. self-registration, login).</summary>
    void SetAuditUser(Guid userUuid, string userName);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string hash, string password);
}

public interface ITokenService
{
    (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(AppUser user);
}

public interface IEmailSender
{
    Task SendAsync(string toEmail, string toName, string subject, string htmlBody, CancellationToken ct = default);
}

public interface ISmsSender
{
    /// <summary>Sends one SMS. Returns the provider message id on success; throws on failure.</summary>
    Task<string?> SendAsync(string normalizedNumber, string message, CancellationToken ct = default);
    bool Enabled { get; }
}

public interface IOrderPdfRenderer
{
    byte[] Render(Orders.OrderDetailDto order, MasterData.CompanyDto company);
}

public sealed class AppOptions
{
    public const string Section = "App";
    /// <summary>Public URL of the app, used in password reset links. Example: https://erp.example.com</summary>
    public string PublicBaseUrl { get; set; } = "http://localhost:4200";
    public string BusinessName { get; set; } = "Sompriti ERP";
}
