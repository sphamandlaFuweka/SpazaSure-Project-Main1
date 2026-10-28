using System.Globalization;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SpazaSure.Infrastructure.Entities;

namespace SpazaSure.PaymentService.Services;

/// <summary>
/// Lays out the invoice as an A4 PDF with what SARS requires on a tax invoice: the words "Tax
/// Invoice", the supplier's name, address and VAT number, the recipient's name and address, a
/// unique serial number, the date, a description, quantity and value of the goods, and the VAT.
/// </summary>
public static class InvoicePdf
{
    private static readonly CultureInfo Za = CultureInfo.GetCultureInfo("en-ZA");
    private static string R(decimal amount) => "R " + amount.ToString("N2", Za);

    public static byte[] Render(TaxInvoice invoice, Order order, string verifyUrl)
    {
        var supplier = order.Supplier;
        var shop = order.Shop;
        var goods = invoice.GoodsTotalInclVat;
        var qrPng = PngByteQRCodeHelper.GetQRCode(verifyUrl, QRCodeGenerator.ECCLevel.M, 6);
        var issued = invoice.IssuedAt.ToLocalTime();
        var blue = Color.FromHex("#25449A");

        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(t => t.FontSize(10).FontColor(Color.FromHex("#17244F")));

            page.Header().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(invoice.IsTaxInvoice ? "TAX INVOICE" : "INVOICE").FontSize(22).Bold().FontColor(blue);
                    col.Item().Text($"Invoice no: {invoice.InvoiceNumber}").SemiBold();
                    col.Item().Text($"Date: {issued.ToString("d MMMM yyyy", Za)}");
                    col.Item().Text($"Order: {order.OrderNumber}");
                });
                row.ConstantItem(90).Image(qrPng);
            });

            page.Content().PaddingVertical(16).Column(col =>
            {
                col.Spacing(14);
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("From (supplier)").FontSize(9).FontColor(Colors.Grey.Darken1);
                        c.Item().Text(supplier.CompanyName).Bold();
                        Line(c, Address(supplier.Address, supplier.City, supplier.Province, supplier.PostalCode));
                        Line(c, supplier.RegistrationNumber is { Length: > 0 } reg ? $"Company reg: {reg}" : null);
                        Line(c, invoice.IsTaxInvoice ? $"VAT no: {invoice.SupplierVatNumber}" : "Not registered for VAT");
                        Line(c, string.Join(" · ", new[] { supplier.Phone, supplier.Email }.Where(s => !string.IsNullOrWhiteSpace(s))));
                        if (order.SupplierBranch is { } b) Line(c, $"Shipped from: {b.Name}, {b.City}");
                    });
                    row.ConstantItem(24);
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("To (recipient)").FontSize(9).FontColor(Colors.Grey.Darken1);
                        c.Item().Text(shop.ShopName).Bold();
                        Line(c, shop.OwnerName);
                        Line(c, order.DeliveryAddress ?? Address(shop.Address, shop.City, shop.Province, shop.PostalCode));
                        Line(c, shop.Phone);
                        Line(c, "Recipient VAT no: not supplied");
                    });
                });

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(5);
                        c.RelativeColumn(1);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                    });
                    table.Header(h =>
                    {
                        foreach (var (text, right) in new[] { ("Description", false), ("Qty", true), ("Unit price", true), ("Amount", true) })
                        {
                            var cell = h.Cell().Background(blue).PaddingVertical(6).PaddingHorizontal(6);
                            var t = right ? cell.AlignRight().Text(text) : cell.Text(text);
                            t.FontColor(Colors.White).SemiBold();
                        }
                    });
                    foreach (var item in order.Items.OrderBy(i => i.Product.Name))
                    {
                        static IContainer Cell(IContainer c) => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5).PaddingHorizontal(6);
                        table.Cell().Element(Cell).Text(t =>
                        {
                            t.Line(item.Product.Name);
                            t.Span($"SKU {item.Product.Sku} · per {item.Product.Unit}").FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                        table.Cell().Element(Cell).AlignRight().Text(item.Quantity.ToString(Za));
                        table.Cell().Element(Cell).AlignRight().Text(R(item.UnitPrice));
                        table.Cell().Element(Cell).AlignRight().Text(R(item.LineTotal));
                    }
                });

                col.Item().AlignRight().Width(240).Column(t =>
                {
                    if (invoice.IsTaxInvoice)
                    {
                        Total(t, "Total excluding VAT", R(goods - invoice.VatAmount));
                        Total(t, "VAT at 15%", R(invoice.VatAmount));
                        Total(t, "Total including VAT", R(goods), bold: true);
                    }
                    else
                    {
                        Total(t, "Total", R(goods), bold: true);
                    }
                });

                if (order.DeliveryFee > 0 || order.PlatformCommission > 0)
                    col.Item().Background(Color.FromHex("#F5F7FF")).Padding(10).Column(c =>
                    {
                        c.Item().Text("Other charges on this order (charged by SpazaSure, not part of this invoice)").FontSize(9).SemiBold();
                        // SpazaSure's platform fee is never shown to shops as a fee of its own
                        c.Item().Text($"Delivery and SpazaSure service: {R(order.DeliveryFee + order.PlatformCommission)}").FontSize(9);
                        c.Item().Text($"Order total paid to SpazaSure: {R(order.TotalAmount)}").FontSize(9);
                    });

                col.Item().Text($"Payment: {PaymentLabel(order.PaymentMethod)} · {order.PaymentStatus}").FontSize(9).FontColor(Colors.Grey.Darken1);
            });

            page.Footer().Column(c =>
            {
                c.Item().Text($"Issued by SpazaSure on behalf of {supplier.CompanyName}. Scan the code to check this invoice is genuine.")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
                c.Item().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                    t.Span("Page ");
                    t.CurrentPageNumber();
                    t.Span(" of ");
                    t.TotalPages();
                });
            });
        })).GeneratePdf();
    }

    private static void Line(ColumnDescriptor c, string? text)
    {
        if (!string.IsNullOrWhiteSpace(text)) c.Item().Text(text);
    }

    private static void Total(ColumnDescriptor c, string label, string value, bool bold = false) =>
        c.Item().PaddingVertical(2).Row(r =>
        {
            var l = r.RelativeItem().Text(label);
            var v = r.AutoItem().Text(value);
            if (bold) { l.Bold(); v.Bold(); }
        });

    private static string Address(params string?[] parts) =>
        string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));

    private static string PaymentLabel(string method) => method switch
    {
        "paystack" => "Card (Paystack)",
        "wallet" => "SpazaSure wallet",
        "cash_on_delivery" => "Cash on delivery",
        "invoice" => "Group buy invoice",
        _ => method,
    };
}
