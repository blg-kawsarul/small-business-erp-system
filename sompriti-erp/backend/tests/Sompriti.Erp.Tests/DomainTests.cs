using Sompriti.Erp.Domain.Common;
using Sompriti.Erp.Domain.Entities;
using Sompriti.Erp.Domain.Enums;
using Sompriti.Erp.Domain.Rules;

namespace Sompriti.Erp.Tests;

public class EnumTextTests
{
    [Fact]
    public void Converts_pascal_case_to_upper_snake_and_back()
    {
        Assert.Equal("MOBILE_BANKING", EnumText.ToText(PaymentMethod.MobileBanking));
        Assert.Equal("OPENING_STOCK", EnumText.ToText(AdjustmentReason.OpeningStock));
        Assert.Equal("DRAFT", EnumText.ToText(PostingStatus.Draft));
        Assert.Equal(PaymentMethod.MobileBanking, EnumText.Parse<PaymentMethod>("MOBILE_BANKING"));
        Assert.Equal(MovementType.SalesVoid, EnumText.Parse<MovementType>("sales_void"));
        Assert.Equal(Role.Manager, EnumText.Parse<Role>("Manager"));
    }

    [Fact]
    public void Rejects_unknown_values()
    {
        Assert.False(EnumText.TryParse<Role>("OWNER", out _));
        Assert.False(EnumText.TryParse<Role>("", out _));
    }
}

public class BdMobileTests
{
    [Theory]
    [InlineData("01712345678", "8801712345678")]
    [InlineData("8801712345678", "8801712345678")]
    [InlineData("+8801912345678", "8801912345678")]
    [InlineData("017-1234 5678", "8801712345678")]
    [InlineData(" 01312345678 ", "8801312345678")]
    public void Normalizes_valid_numbers(string input, string expected) => Assert.Equal(expected, BdMobile.Normalize(input));

    [Theory]
    [InlineData("01212345678")]   // operator digit 2 is not valid
    [InlineData("0171234567")]    // too short
    [InlineData("017123456789")]  // too long
    [InlineData("60123456789")]   // Malaysian number
    [InlineData("abc")]
    [InlineData("")]
    public void Rejects_invalid_numbers(string input) => Assert.Null(BdMobile.Normalize(input));

    [Fact]
    public void Displays_local_format() => Assert.Equal("01712345678", BdMobile.ToDisplay("8801712345678"));
}

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("abcdefg1", true)]
    [InlineData("12345678", false)]
    [InlineData("abcdefgh", false)]
    [InlineData("ab1", false)]
    public void Enforces_length_letter_and_digit(string password, bool valid) => Assert.Equal(valid, PasswordPolicy.IsValid(password));
}

public class PostingRulesTests
{
    [Theory]
    [InlineData(PostingStatus.Draft, PostingStatus.Final, true)]
    [InlineData(PostingStatus.Draft, PostingStatus.Void, true)]
    [InlineData(PostingStatus.Final, PostingStatus.Void, true)]
    [InlineData(PostingStatus.Final, PostingStatus.Draft, false)]
    [InlineData(PostingStatus.Void, PostingStatus.Final, false)]
    [InlineData(PostingStatus.Void, PostingStatus.Draft, false)]
    [InlineData(PostingStatus.Final, PostingStatus.Final, false)]
    public void Allows_only_defined_transitions(PostingStatus from, PostingStatus to, bool allowed) =>
        Assert.Equal(allowed, PostingRules.CanTransition(from, to));

    [Fact]
    public void Invalid_transition_throws_conflict()
    {
        var ex = Assert.Throws<DomainException>(() => PostingRules.EnsureTransition(PostingStatus.Void, PostingStatus.Final));
        Assert.Equal(ErrorKind.Conflict, ex.Kind);
        Assert.Equal(ErrorCodes.InvalidStatusTransition, ex.Code);
    }

    [Fact]
    public void Stock_direction_is_positive_for_purchase_and_negative_for_sales()
    {
        Assert.Equal(1, PostingRules.FinalizeSign(TransactionType.Purchase));
        Assert.Equal(-1, PostingRules.FinalizeSign(TransactionType.Sales));
    }
}

public class LineCalculatorTests
{
    private static readonly Product BoxProduct = new()
    {
        Uuid = Guid.NewGuid(), ProductCode = "P-BOX", ProductName = "Box product", Uom = Uom.Box, PcsPerBox = 5,
        ProductPurchasePrice = 10m, ProductSalesPrice = 12.5m
    };

    private static readonly Product PcsProduct = new()
    {
        Uuid = Guid.NewGuid(), ProductCode = "P-PCS", ProductName = "Pcs product", Uom = Uom.Pcs,
        ProductPurchasePrice = 3.333m, ProductSalesPrice = 4m
    };

    private static LineValues Calc(LineInput input, Product product, decimal price, out Validator v)
    {
        v = new Validator();
        return LineCalculator.Normalize(input, product, price, "lines[0]", v);
    }

    [Fact]
    public void Srs_example_three_boxes_of_five_equals_fifteen_pcs()
    {
        var r = Calc(new LineInput(null, BoxProduct.Uuid, QuantityType.Box, 3, null, null, null, null, null), BoxProduct, 10m, out var v);
        Assert.False(v.HasErrors);
        Assert.Equal(15, r.TotalQuantityPcs);
        Assert.Equal(10m, r.PerPcsPrice);
        Assert.Equal(50m, r.PerBoxPrice);
        Assert.Equal(150m, r.TotalPrice);
        Assert.Equal(5, r.PcsPerBoxSnapshot);
    }

    [Fact]
    public void Pcs_line_uses_default_price_and_rounds_half_away_from_zero()
    {
        var r = Calc(new LineInput(null, PcsProduct.Uuid, QuantityType.Pcs, null, 3, null, null, null, null), PcsProduct, 3.335m, out var v);
        Assert.False(v.HasErrors);
        Assert.Equal(3, r.TotalQuantityPcs);
        Assert.Equal(3.34m, r.PerPcsPrice);
        Assert.Null(r.PerBoxPrice);
        Assert.Equal(10.02m, r.TotalPrice);
    }

    [Fact]
    public void User_entered_values_are_preserved()
    {
        // 3 boxes plus 2 loose pcs, custom box price, custom total
        var r = Calc(new LineInput(null, BoxProduct.Uuid, QuantityType.Box, 3, null, 17, 9.5m, 47.5m, 160m), BoxProduct, 10m, out var v);
        Assert.False(v.HasErrors);
        Assert.Equal(17, r.TotalQuantityPcs);
        Assert.Equal(9.5m, r.PerPcsPrice);
        Assert.Equal(47.5m, r.PerBoxPrice);
        Assert.Equal(160m, r.TotalPrice);
    }

    [Fact]
    public void Per_box_price_only_derives_per_pcs_price()
    {
        var r = Calc(new LineInput(null, BoxProduct.Uuid, QuantityType.Box, 2, null, null, null, 60m, null), BoxProduct, 10m, out var v);
        Assert.False(v.HasErrors);
        Assert.Equal(12m, r.PerPcsPrice);
        Assert.Equal(60m, r.PerBoxPrice);
        Assert.Equal(120m, r.TotalPrice);
    }

    [Fact]
    public void Box_quantity_type_requires_pcs_per_box()
    {
        Calc(new LineInput(null, PcsProduct.Uuid, QuantityType.Box, 2, null, null, null, null, null), PcsProduct, 4m, out var v);
        Assert.True(v.HasErrors);
        var ex = Assert.Throws<DomainException>(() => v.ThrowIfInvalid());
        Assert.Contains("lines[0].quantityType", ex.Errors.Keys);
    }

    [Fact]
    public void Quantities_must_be_positive_and_prices_non_negative()
    {
        Calc(new LineInput(null, PcsProduct.Uuid, QuantityType.Pcs, null, 0, null, -1m, null, null), PcsProduct, 4m, out var v);
        var ex = Assert.Throws<DomainException>(() => v.ThrowIfInvalid());
        Assert.Contains("lines[0].pcsQuantity", ex.Errors.Keys);
        Assert.Contains("lines[0].totalQuantityPcs", ex.Errors.Keys);
        Assert.Contains("lines[0].perPcsPrice", ex.Errors.Keys);
    }

    [Fact]
    public void Quantity_by_product_sums_active_lines_only()
    {
        var p = Guid.NewGuid();
        var lines = new List<OrderLine>
        {
            new SalesOrderLineItem { ProductUuid = p, TotalQuantityPcs = 5 },
            new SalesOrderLineItem { ProductUuid = p, TotalQuantityPcs = 7 },
            new SalesOrderLineItem { ProductUuid = p, TotalQuantityPcs = 100, Status = RecordStatus.Deleted },
        };
        var result = LineCalculator.QuantityByProduct(lines);
        Assert.Equal(12, result[p]);
    }
}

public class SmsTemplateTests
{
    [Fact]
    public void Sales_message_contains_amount_and_due_and_fits_one_segment()
    {
        var text = SmsTemplates.Payment(TransactionType.Sales, "Sompriti Enterprise", "100023", 1500m, 2500.5m);
        Assert.Contains("Tk 1,500", text);
        Assert.Contains("Due: Tk 2,500.5", text);
        Assert.Contains("#100023", text);
        Assert.True(text.Length <= 160, $"length {text.Length}");
    }

    [Fact]
    public void Purchase_message_uses_supplier_wording()
    {
        var text = SmsTemplates.Payment(TransactionType.Purchase, "A very long company name that goes on and on", "100001", 10m, 0m);
        Assert.Contains("made for Purchase #100001", text);
        Assert.True(text.Length <= 160);
    }
}

public class BusinessClockTests
{
    [Fact]
    public void Today_uses_bangladesh_time()
    {
        // 2026-01-01 20:00 UTC is 2026-01-02 02:00 in Dhaka
        var utc = new DateTimeOffset(2026, 1, 1, 20, 0, 0, TimeSpan.Zero);
        Assert.Equal(new DateOnly(2026, 1, 2), BusinessClock.Today(utc));
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 18, 0, 0, TimeSpan.Zero), BusinessClock.StartOfDayUtc(new DateOnly(2026, 1, 2)));
    }
}
