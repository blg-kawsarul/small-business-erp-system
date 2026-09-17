using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Sompriti.Erp.Application.Common;
using Sompriti.Erp.Domain.Common;
using Sompriti.Erp.Domain.Entities;
using Sompriti.Erp.Domain.Enums;

namespace Sompriti.Erp.Infrastructure.Persistence;

/// <summary>
/// EF Core context. The schema itself is created by the SQL scripts in Persistence/Migrations
/// (see MigrationRunner); this class only maps entities to that schema.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser currentUser, TimeProvider clock)
    : DbContext(options), IAppDbContext
{
    private Guid? _auditUserUuid;
    private string? _auditUserName;

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockBalance> StockBalances => Set<StockBalance>();
    public DbSet<StockLedger> StockLedgers => Set<StockLedger>();
    public DbSet<StockAdjustment> StockAdjustments => Set<StockAdjustment>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLineItem> PurchaseOrderLines => Set<PurchaseOrderLineItem>();
    public DbSet<PurchaseOrderPayment> PurchaseOrderPayments => Set<PurchaseOrderPayment>();
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<SalesOrderLineItem> SalesOrderLines => Set<SalesOrderLineItem>();
    public DbSet<SalesOrderPayment> SalesOrderPayments => Set<SalesOrderPayment>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<SmsOutbox> SmsOutbox => Set<SmsOutbox>();

    public void SetOriginalRevision(AuditedEntity entity, Guid revision) =>
        Entry(entity).Property(nameof(AuditedEntity.Revision)).OriginalValue = revision;

    public void SetAuditUser(Guid userUuid, string userName)
    {
        _auditUserUuid = userUuid;
        _auditUserName = userName;
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAudit();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        StampAudit();
        return base.SaveChanges();
    }

    private void StampAudit()
    {
        var now = clock.GetUtcNow();
        var (userUuid, userName) = currentUser.IsAuthenticated
            ? (currentUser.UserUuid, currentUser.UserName)
            : (_auditUserUuid ?? AppUser.SystemUserUuid, _auditUserName ?? AppUser.SystemUserName);

        foreach (var entry in ChangeTracker.Entries<AuditedEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.Revision = Guid.NewGuid();
                    entry.Entity.CreatedDate = now;
                    entry.Entity.UpdatedDate = now;
                    entry.Entity.CreatedByUserUuid = userUuid;
                    entry.Entity.UpdatedByUserUuid = userUuid;
                    entry.Entity.CreatedByUserName = userName;
                    entry.Entity.UpdatedByUserName = userName;
                    break;
                case EntityState.Modified:
                    // The original revision stays as the concurrency token; the stored revision is regenerated.
                    entry.Entity.Revision = Guid.NewGuid();
                    entry.Entity.UpdatedDate = now;
                    entry.Entity.UpdatedByUserUuid = userUuid;
                    entry.Entity.UpdatedByUserName = userName;
                    break;
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>(e => { e.ToTable("app_user"); Audited(e); });
        b.Entity<Company>(e => { e.ToTable("company"); Audited(e); });

        b.Entity<Customer>(e =>
        {
            e.ToTable("customer"); Audited(e);
            e.Property(x => x.Name).HasColumnName("customer_name");
            e.Property(x => x.Code).HasColumnName("customer_code").ValueGeneratedOnAdd().HasDefaultValueSql("nextval('customer_code_seq')::text");
        });
        b.Entity<Supplier>(e =>
        {
            e.ToTable("supplier"); Audited(e);
            e.Property(x => x.Name).HasColumnName("supplier_name");
            e.Property(x => x.Code).HasColumnName("supplier_code").ValueGeneratedOnAdd().HasDefaultValueSql("nextval('supplier_code_seq')::text");
        });

        b.Entity<Product>(e => { e.ToTable("product"); Audited(e); });
        b.Entity<StockBalance>(e => { e.ToTable("stock_balance"); Audited(e); });
        b.Entity<StockLedger>(e => { e.ToTable("stock_ledger"); e.HasKey(x => x.Uuid); });
        b.Entity<StockAdjustment>(e =>
        {
            e.ToTable("stock_adjustment"); Audited(e);
            e.Property(x => x.AdjustmentNumber).ValueGeneratedOnAdd().HasDefaultValueSql("nextval('stock_adjustment_number_seq')::text");
        });

        Order<PurchaseOrder>(b, "purchase_order", "purchase_order_number", "supplier_uuid", "purchase_order_number_seq");
        Line<PurchaseOrderLineItem>(b, "purchase_order_line_item", "purchase_order_uuid");
        Payment<PurchaseOrderPayment>(b, "purchase_order_payment", "purchase_order_uuid");
        Order<SalesOrder>(b, "sales_order", "sales_order_number", "customer_uuid", "sales_order_number_seq");
        Line<SalesOrderLineItem>(b, "sales_order_line_item", "sales_order_uuid");
        Payment<SalesOrderPayment>(b, "sales_order_payment", "sales_order_uuid");

        b.Entity<RefreshToken>(e => { e.ToTable("refresh_token"); e.HasKey(x => x.Uuid); });
        b.Entity<PasswordResetToken>(e => { e.ToTable("password_reset_token"); e.HasKey(x => x.Uuid); });
        b.Entity<SmsOutbox>(e => { e.ToTable("sms_outbox"); e.HasKey(x => x.Uuid); });

        // Conventions: snake_case columns unless already set, enums stored as UPPER_SNAKE text.
        foreach (var entity in b.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                if (property.GetColumnName() == property.Name)
                    property.SetColumnName(ToSnakeCase(property.Name));

                var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (type.IsEnum)
                {
                    var converterType = typeof(EnumTextConverter<>).MakeGenericType(type);
                    property.SetValueConverter((ValueConverter)Activator.CreateInstance(converterType)!);
                }
            }
        }
    }

    private static void Audited<T>(EntityTypeBuilder<T> e) where T : AuditedEntity
    {
        e.HasKey(x => x.Uuid);
        e.Property(x => x.Revision).IsConcurrencyToken();
        if (typeof(SoftDeletableEntity).IsAssignableFrom(typeof(T)))
            e.Ignore(nameof(SoftDeletableEntity.IsActive));
    }

    private static void Order<T>(ModelBuilder b, string table, string numberColumn, string partyColumn, string sequence) where T : OrderHeader
    {
        b.Entity<T>(e =>
        {
            e.ToTable(table); Audited(e);
            e.Property(x => x.OrderNumber).HasColumnName(numberColumn).ValueGeneratedOnAdd().HasDefaultValueSql($"nextval('{sequence}')::text");
            e.Property(x => x.PartyUuid).HasColumnName(partyColumn);
            e.Ignore(x => x.DueAmount);
        });
    }

    private static void Line<T>(ModelBuilder b, string table, string orderColumn) where T : OrderLine
    {
        b.Entity<T>(e =>
        {
            e.ToTable(table); Audited(e);
            e.Property(x => x.OrderUuid).HasColumnName(orderColumn);
        });
    }

    private static void Payment<T>(ModelBuilder b, string table, string orderColumn) where T : OrderPayment
    {
        b.Entity<T>(e =>
        {
            e.ToTable(table); Audited(e);
            e.Property(x => x.OrderUuid).HasColumnName(orderColumn);
        });
    }

    public static string ToSnakeCase(string name)
    {
        var sb = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c) && i > 0) sb.Append('_');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}

/// <summary>Stores enums as UPPER_SNAKE_CASE text, e.g. PaymentMethod.MobileBanking -> "MOBILE_BANKING".</summary>
public sealed class EnumTextConverter<TEnum>() : ValueConverter<TEnum, string>(
    v => EnumText.ToText(v.ToString()),
    v => EnumText.Parse<TEnum>(v)) where TEnum : struct, Enum;
