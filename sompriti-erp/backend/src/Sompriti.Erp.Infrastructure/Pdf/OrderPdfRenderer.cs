using System.Globalization;
using Sompriti.Erp.Application.Common;
using Sompriti.Erp.Application.MasterData;
using Sompriti.Erp.Application.Orders;
using Sompriti.Erp.Domain.Common;
using Sompriti.Erp.Domain.Enums;
using Sompriti.Erp.Domain.Rules;
using static Sompriti.Erp.Infrastructure.Pdf.SimplePdf;

namespace Sompriti.Erp.Infrastructure.Pdf;

/// <summary>Renders a purchase order / sales invoice as an A4 PDF (SRS 11.4).</summary>
public sealed class OrderPdfRenderer(ICurrentUser currentUser, TimeProvider clock) : IOrderPdfRenderer
{
    private const float Margin = 36;
    private const float Right = PageWidth - Margin;
    private const float ContentWidth = PageWidth - Margin * 2;
    private const float BottomLimit = PageHeight - 70;
    private const float RowHeight = 17;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private sealed record Column(string Title, float Width, Align Align);

    public byte[] Render(OrderDetailDto order, CompanyDto company)
    {
        var pdf = new SimplePdf();
        var isSales = order.TransactionType == TransactionType.Sales;
        var watermark = order.PostingStatus switch { PostingStatus.Draft => "DRAFT", PostingStatus.Void => "VOID", _ => null };
        if (watermark is not null) pdf.Watermark(watermark);

        // ---------------------------------------------------------------- header
        var y = Margin;
        pdf.Text(Margin, y, Fit(company.CompanyName, 300, 16, true), 16, bold: true);
        pdf.Text(Right, y + 2, isSales ? "SALES INVOICE" : "PURCHASE ORDER", 14, bold: true, align: Align.Right);
        y += 22;

        var companyLines = new List<string>();
        var address = string.Join(", ", new[] { company.AddressLine, company.City, company.State, company.PostalCode }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (address.Length > 0) companyLines.AddRange(Wrap(address, 300, 9));
        var contact = string.Join("   ", new[] { Prefix("Phone: ", company.PhoneNumber), Prefix("Email: ", company.Email) }.Where(s => s is not null));
        if (contact.Length > 0) companyLines.Add(contact);
        if (!string.IsNullOrWhiteSpace(company.LicenseNumber)) companyLines.Add("License No: " + company.LicenseNumber);
        companyLines.Add("Company Code: " + company.CompanyCode);

        var metaLines = new[]
        {
            ("No:", order.OrderNumber),
            ("Date:", order.OrderDate.ToString("dd MMM yyyy", Inv)),
            ("Status:", EnumText.ToText(order.PostingStatus)),
            ("Payment Type:", EnumText.ToText(order.PaymentType)),
        };

        var metaY = y;
        foreach (var line in companyLines) { pdf.Text(Margin, y, line, 9, gray: 0.2f); y += 12; }
        foreach (var (label, value) in metaLines)
        {
            pdf.Text(Right - 150, metaY, label, 9, gray: 0.35f);
            pdf.Text(Right, metaY, value, 9, bold: true, align: Align.Right);
            metaY += 13;
        }
        y = Math.Max(y, metaY) + 8;
        pdf.Line(Margin, y, Right, y, 1, 0.2f);
        y += 10;

        // ---------------------------------------------------------------- party
        pdf.Text(Margin, y, isSales ? "BILL TO" : "SUPPLIER", 8, bold: true, gray: 0.4f);
        y += 12;
        pdf.Text(Margin, y, Fit($"{order.Party.Name} ({order.Party.Code})", ContentWidth, 11, true), 11, bold: true);
        y += 15;
        pdf.Text(Margin, y, "Mobile: " + order.Party.MobileNumber, 9, gray: 0.2f);
        y += 12;
        var partyAddress = string.Join(", ", new[] { order.Party.Address, order.Party.City }.Where(s => !string.IsNullOrWhiteSpace(s)));
        foreach (var line in Wrap(partyAddress, ContentWidth, 9).Where(l => l.Length > 0)) { pdf.Text(Margin, y, line, 9, gray: 0.2f); y += 12; }
        y += 10;

        // ---------------------------------------------------------------- lines table
        var columns = new List<Column>
        {
            new("SL", 22, Align.Left),
            new("Code", 62, Align.Left),
            new("Product", 0, Align.Left),
            new("Type", 30, Align.Left),
            new("Boxes", 36, Align.Right),
            new("Pcs", 44, Align.Right),
            new("Box Price", 58, Align.Right),
            new("Pcs Price", 56, Align.Right),
            new("Total", 70, Align.Right),
        };
        var fixedWidth = columns.Sum(c => c.Width);
        columns[2] = columns[2] with { Width = ContentWidth - fixedWidth };

        y = TableHeader(pdf, columns, y);
        var sl = 0;
        foreach (var line in order.Lines)
        {
            if (y + RowHeight > BottomLimit)
            {
                pdf.NewPage();
                if (watermark is not null) pdf.Watermark(watermark);
                y = TableHeader(pdf, columns, Margin);
            }
            var cells = new[]
            {
                (++sl).ToString(Inv),
                line.ProductCode,
                line.ProductName,
                EnumText.ToText(line.QuantityType),
                line.BoxQuantity?.ToString("#,##0", Inv) ?? "-",
                line.TotalQuantityPcs.ToString("#,##0", Inv),
                line.PerBoxPrice is { } pb ? Money.FormatPlain(pb) : "-",
                Money.FormatPlain(line.PerPcsPrice),
                Money.FormatPlain(line.TotalPrice),
            };
            Row(pdf, columns, cells, y, bold: false);
            y += RowHeight;
            pdf.Line(Margin, y, Right, y, 0.3f, 0.8f);
        }
        if (order.Lines.Count == 0)
        {
            pdf.Text(Margin + 4, y + 4, "No line items.", 9, gray: 0.4f);
            y += RowHeight;
        }

        // ---------------------------------------------------------------- totals
        y += 8;
        if (y + 60 > BottomLimit) { pdf.NewPage(); if (watermark is not null) pdf.Watermark(watermark); y = Margin; }
        foreach (var (label, value, strong) in new[]
                 {
                     ("Order Total", order.TotalAmount, true),
                     ("Total Paid", order.TotalPaidAmount, false),
                     ("Due Amount", order.DueAmount, true),
                 })
        {
            pdf.Text(Right - 120, y, label, 10, bold: strong, gray: strong ? 0 : 0.3f);
            pdf.Text(Right, y, Money.Format(value), 10, bold: strong, align: Align.Right);
            y += 15;
        }
        y += 10;

        // ---------------------------------------------------------------- payments
        var payColumns = new List<Column>
        {
            new("Date", 80, Align.Left),
            new("Method", 100, Align.Left),
            new("Note", 0, Align.Left),
            new("Recorded By", 110, Align.Left),
            new("Amount", 90, Align.Right),
        };
        payColumns[2] = payColumns[2] with { Width = ContentWidth - payColumns.Sum(c => c.Width) };

        if (y + 50 > BottomLimit) { pdf.NewPage(); if (watermark is not null) pdf.Watermark(watermark); y = Margin; }
        pdf.Text(Margin, y, "PAYMENT HISTORY", 9, bold: true, gray: 0.3f);
        y += 14;
        if (order.Payments.Count == 0)
        {
            pdf.Text(Margin, y, "No payments recorded.", 9, gray: 0.4f);
            y += 14;
        }
        else
        {
            y = TableHeader(pdf, payColumns, y);
            foreach (var p in order.Payments)
            {
                if (y + RowHeight > BottomLimit)
                {
                    pdf.NewPage();
                    if (watermark is not null) pdf.Watermark(watermark);
                    y = TableHeader(pdf, payColumns, Margin);
                }
                Row(pdf, payColumns, new[]
                {
                    p.PaymentDate.ToString("dd MMM yyyy", Inv),
                    EnumText.ToText(p.PaymentMethod).Replace('_', ' '),
                    p.PaymentNote ?? "",
                    p.CreatedByUserName,
                    Money.FormatPlain(p.PaymentAmount),
                }, y, bold: false);
                y += RowHeight;
                pdf.Line(Margin, y, Right, y, 0.3f, 0.8f);
            }
        }

        // ---------------------------------------------------------------- notes / void reason
        var extras = new List<(string Title, string Text)>();
        if (!string.IsNullOrWhiteSpace(order.Notes)) extras.Add(("NOTES", order.Notes));
        if (order.PostingStatus == PostingStatus.Void && !string.IsNullOrWhiteSpace(order.VoidReason))
            extras.Add(("VOID REASON", order.VoidReason));
        foreach (var (title, text) in extras)
        {
            var wrapped = Wrap(text, ContentWidth, 9);
            y += 12;
            if (y + 14 + wrapped.Count * 12 > BottomLimit) { pdf.NewPage(); if (watermark is not null) pdf.Watermark(watermark); y = Margin; }
            pdf.Text(Margin, y, title, 9, bold: true, gray: 0.3f);
            y += 13;
            foreach (var line in wrapped) { pdf.Text(Margin, y, line, 9); y += 12; }
        }

        // ---------------------------------------------------------------- footer on every page
        var printed = clock.GetUtcNow().ToOffset(BusinessClock.Offset).ToString("dd MMM yyyy hh:mm tt", Inv);
        var by = currentUser.IsAuthenticated ? currentUser.UserName : "system";
        var total = pdf.PageCount;
        for (var i = 0; i < total; i++)
        {
            var pageNo = i + 1;
            pdf.DrawOnPage(i, p =>
            {
                p.Line(Margin, PageHeight - 44, Right, PageHeight - 44, 0.5f, 0.7f);
                p.Text(Margin, PageHeight - 38, $"Printed on {printed} by {by}", 8, gray: 0.4f);
                p.Text(Right, PageHeight - 38, $"Page {pageNo} of {total}", 8, gray: 0.4f, align: Align.Right);
            });
        }

        return pdf.Build();
    }

    private static float TableHeader(SimplePdf pdf, List<Column> columns, float y)
    {
        pdf.FillRect(Margin, y, ContentWidth, RowHeight + 1, 0.92f);
        Row(pdf, columns, columns.Select(c => c.Title).ToArray(), y, bold: true);
        return y + RowHeight + 1;
    }

    private static void Row(SimplePdf pdf, List<Column> columns, string[] cells, float y, bool bold)
    {
        var x = Margin;
        for (var i = 0; i < columns.Count; i++)
        {
            var col = columns[i];
            var text = Fit(cells[i], col.Width - 8, 8.5f, bold);
            var tx = col.Align == Align.Right ? x + col.Width - 4 : x + 4;
            pdf.Text(tx, y + 4.5f, text, 8.5f, bold, col.Align);
            x += col.Width;
        }
    }

    private static string? Prefix(string prefix, string? value) => string.IsNullOrWhiteSpace(value) ? null : prefix + value;
}
