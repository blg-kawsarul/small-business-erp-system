using System.Text;
using Microsoft.Extensions.Options;
using Sompriti.Erp.Application.Common;
using Sompriti.Erp.Application.MasterData;
using Sompriti.Erp.Application.Orders;
using Sompriti.Erp.Domain.Entities;
using Sompriti.Erp.Domain.Enums;
using Sompriti.Erp.Infrastructure;
using Sompriti.Erp.Infrastructure.Pdf;
using Sompriti.Erp.Infrastructure.Persistence;
using Sompriti.Erp.Infrastructure.Security;

namespace Sompriti.Erp.Tests;

internal sealed class FakeClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class FakeUser : ICurrentUser
{
    public bool IsAuthenticated => true;
    public Guid UserUuid { get; } = Guid.NewGuid();
    public string UserName => "Test Admin";
    public Role Role => Role.Admin;
    public Guid? SupplierUuid => null;
    public Guid? CustomerUuid => null;
}

public class JwtTests
{
    private static JwtTokenService Create(FakeClock clock, string key = "unit-test-signing-key-0123456789abcdef") =>
        new(Options.Create(new JwtOptions { SigningKey = key, AccessTokenMinutes = 15 }), clock);

    [Fact]
    public void Created_token_validates_and_carries_user()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var svc = Create(clock);
        var user = new AppUser { Uuid = Guid.NewGuid(), UserName = "Rahim", Role = Role.Manager };
        var (token, exp) = svc.CreateAccessToken(user);
        var claims = svc.Validate(token);
        Assert.NotNull(claims);
        Assert.Equal(user.Uuid, claims!.UserUuid);
        Assert.Equal("Rahim", claims.UserName);
        Assert.True(exp > clock.Now);
    }

    [Fact]
    public void Expired_token_is_rejected()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var svc = Create(clock);
        var (token, _) = svc.CreateAccessToken(new AppUser { Uuid = Guid.NewGuid(), UserName = "x" });
        clock.Now = clock.Now.AddMinutes(16);
        Assert.Null(svc.Validate(token));
    }

    [Fact]
    public void Tampered_or_foreign_tokens_are_rejected()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var svc = Create(clock);
        var (token, _) = svc.CreateAccessToken(new AppUser { Uuid = Guid.NewGuid(), UserName = "x" });
        var parts = token.Split('.');
        var tampered = parts[0] + "." + parts[1].Replace('A', 'B') + "." + parts[2];
        if (tampered != token) Assert.Null(svc.Validate(tampered));
        Assert.Null(Create(clock, "another-signing-key-0123456789abcdefgh").Validate(token));
        Assert.Null(svc.Validate("not.a.token"));
        Assert.Null(svc.Validate(""));
    }

    [Fact]
    public void Short_signing_key_is_refused() =>
        Assert.Throws<InvalidOperationException>(() => Create(new FakeClock(DateTimeOffset.UtcNow), "short"));
}

public class PasswordHasherTests
{
    [Fact]
    public void Hash_verifies_only_the_right_password()
    {
        var hasher = new IdentityPasswordHasher();
        var hash = hasher.Hash("Secret123");
        Assert.NotEqual("Secret123", hash);
        Assert.True(hasher.Verify(hash, "Secret123"));
        Assert.False(hasher.Verify(hash, "secret123"));
        Assert.False(hasher.Verify("garbage", "Secret123"));
    }
}

public class ConnectionStringTests
{
    [Fact]
    public void Converts_railway_database_url()
    {
        var cs = ConnectionStrings.FromUrl("postgresql://postgres:p%40ss@containers.railway.app:6543/railway");
        Assert.Contains("Host=containers.railway.app", cs);
        Assert.Contains("Port=6543", cs);
        Assert.Contains("Database=railway", cs);
        Assert.Contains("Username=postgres", cs);
        Assert.Contains("Password=p@ss", cs);
    }

    [Fact]
    public void Snake_case_column_names() =>
        Assert.Equal("created_by_user_uuid", AppDbContext.ToSnakeCase("CreatedByUserUuid"));
}

public class PdfTests
{
    internal static OrderDetailDto SampleOrder(int lineCount, PostingStatus status)
    {
        var lines = Enumerable.Range(1, lineCount).Select(i => new OrderLineDto(Guid.NewGuid(), i, Guid.NewGuid(), $"P-{i:000}",
            $"Product number {i} with a fairly long descriptive name (500ml)", i % 2 == 0 ? QuantityType.Box : QuantityType.Pcs,
            i % 2 == 0 ? 3 : null, i % 2 == 0 ? null : 7, i % 2 == 0 ? 12 : null, i % 2 == 0 ? 36 : 7,
            12.5m, i % 2 == 0 ? 150m : null, i % 2 == 0 ? 450m : 87.5m)).ToList();
        var total = lines.Sum(l => l.TotalPrice);
        var payments = new List<OrderPaymentDto>
        {
            new(Guid.NewGuid(), new DateOnly(2026, 9, 10), 1000m, PaymentMethod.MobileBanking, "bKash (ref 8XK2)", DateTimeOffset.UtcNow, "Admin"),
            new(Guid.NewGuid(), new DateOnly(2026, 9, 12), 500m, PaymentMethod.Cash, null, DateTimeOffset.UtcNow, "Manager"),
        };
        return new OrderDetailDto(Guid.NewGuid(), TransactionType.Sales, "100042",
            new OrderCompanyDto(Guid.NewGuid(), "Sompriti Enterprise", "SE"),
            new OrderPartyDto(Guid.NewGuid(), "Karim Traders", "100007", "01712345678", "House 12, Road 5, Dhanmondi", "Dhaka"),
            PaymentType.Installment, status, new DateOnly(2026, 9, 9), "Deliver before Friday. Handle with care.",
            total, 1500m, total - 1500m, null, null, null, null, status == PostingStatus.Void ? "Customer cancelled" : null,
            Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "Admin", "Admin", lines, payments);
    }

    internal static CompanyDto SampleCompany() => new(Guid.NewGuid(), "Sompriti Enterprise", "SE", "12/A Motijheel C/A", "Dhaka", "Dhaka",
        "1000", "01811111111", "info@sompriti.com", "TRAD/DSCC/12345", Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "a", "a");

    [Fact]
    public void Renders_a_valid_single_page_pdf()
    {
        var renderer = new OrderPdfRenderer(new FakeUser(), new FakeClock(DateTimeOffset.UtcNow));
        var bytes = renderer.Render(SampleOrder(5, PostingStatus.Final), SampleCompany());
        var text = Encoding.Latin1.GetString(bytes);
        Assert.StartsWith("%PDF-1.4", text);
        Assert.Contains("%%EOF", text);
        Assert.Contains("/Count 1", text);
        Assert.Contains("SALES INVOICE", text);
        Assert.DoesNotContain("DRAFT", text);
    }

    [Fact]
    public void Long_orders_span_multiple_pages_with_watermark()
    {
        var renderer = new OrderPdfRenderer(new FakeUser(), new FakeClock(DateTimeOffset.UtcNow));
        var bytes = renderer.Render(SampleOrder(80, PostingStatus.Draft), SampleCompany());
        var text = Encoding.Latin1.GetString(bytes);
        Assert.Contains("/Count 3", text);
        Assert.Contains("(DRAFT) Tj", text);
        Assert.Contains("Page 3 of 3", text);
    }
}
