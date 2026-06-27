using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using IzaleSparkle.Application.Common.Interfaces;

namespace IzaleSparkle.Infrastructure.Notifications;

/// <summary>
/// Generates a professional branded PDF invoice using QuestPDF (Community — free).
/// Returns the PDF as a byte array ready to attach to an email.
/// </summary>
public static class InvoiceGenerator
{
    // Brand colours as strings (QuestPDF Background/BorderColor accept hex strings)
    private const string Gold       = "#C8973A";
    private const string Charcoal   = "#1C1A16";
    private const string CreamSoft  = "#FAF7EE";
    private const string White      = "#FFFFFF";   // ← plain string, not Colors.White
    private const string WarmGray   = "#7A6E5F";
    private const string BorderGold = "#E8DFC4";
    private const string GreenOk    = "#16a34a";

    public static byte[] Generate(OrderEmailData d)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(10).FontColor(Charcoal));

                // ── HEADER ────────────────────────────────────────────
                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(brand =>
                        {
                            brand.Item()
                                .Text("Izale  Sparkle")
                                .FontSize(22).FontColor(Gold).Bold();
                            brand.Item()
                                .Text("WHERE EVERY LOOK SPARKLES")
                                .FontSize(7).FontColor(WarmGray);
                            brand.Item().PaddingTop(4)
                                .Text("45 Ryecroft, Haywards Heath, RH16 4NW")
                                .FontSize(8).FontColor(WarmGray);
                            brand.Item()
                                .Text("info@izalesparkle.com  ·  izalesparkle.com")
                                .FontSize(8).FontColor(WarmGray);
                        });

                        row.ConstantItem(160).Column(inv =>
                        {
                            inv.Item().Background(Charcoal).Padding(12).Column(inner =>
                            {
                                inner.Item()
                                    .Text("INVOICE")
                                    .FontSize(18).FontColor(Gold).Bold().AlignRight();
                                inner.Item().PaddingTop(6)
                                    .Text(d.OrderNumber)
                                    .FontSize(9).FontColor(CreamSoft).AlignRight();
                                inner.Item()
                                    .Text(DateTime.UtcNow.ToString("dd MMMM yyyy"))
                                    .FontSize(8).FontColor(WarmGray).AlignRight();
                            });
                        });
                    });

                    col.Item().PaddingTop(8).LineHorizontal(1.5f).LineColor(Gold);
                });

                // ── CONTENT ───────────────────────────────────────────
                page.Content().PaddingVertical(16).Column(col =>
                {
                    // Bill To / Ship To / Invoice Details
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(bill =>
                        {
                            bill.Item().Text("BILL TO").FontSize(8).FontColor(Gold).Bold();
                            bill.Item().PaddingTop(4).Text(d.CustomerName).Bold();
                            bill.Item().Text(d.CustomerEmail).FontColor(WarmGray);
                        });

                        row.RelativeItem().Column(ship =>
                        {
                            ship.Item().Text("SHIP TO").FontSize(8).FontColor(Gold).Bold();
                            ship.Item().PaddingTop(4).Text(text =>
                            {
                                foreach (var line in d.ShippingAddress
                                    .Split('\n', StringSplitOptions.RemoveEmptyEntries))
                                    text.Line(line.Trim());
                            });
                        });

                        row.RelativeItem().Column(info =>
                        {
                            info.Item().Text("INVOICE DETAILS").FontSize(8).FontColor(Gold).Bold();
                            info.Item().PaddingTop(4).Table(t =>
                            {
                                t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
                                t.Cell().Text("Invoice No:").FontColor(WarmGray).FontSize(9);
                                t.Cell().Text(d.OrderNumber).FontSize(9).AlignRight();
                                t.Cell().Text("Date:").FontColor(WarmGray).FontSize(9);
                                t.Cell().Text(DateTime.UtcNow.ToString("dd/MM/yyyy")).FontSize(9).AlignRight();
                                t.Cell().Text("Shipping:").FontColor(WarmGray).FontSize(9);
                                t.Cell().Text(d.ShippingTier).FontSize(9).AlignRight();
                                if (!string.IsNullOrEmpty(d.PromoCode))
                                {
                                    t.Cell().Text("Promo:").FontColor(WarmGray).FontSize(9);
                                    t.Cell().Text(d.PromoCode).FontSize(9).AlignRight();
                                }
                            });
                        });
                    });

                    col.Item().PaddingVertical(16).LineHorizontal(0.5f).LineColor(BorderGold);

                    // Items table
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(28);
                            c.RelativeColumn(4);
                            c.RelativeColumn();
                            c.RelativeColumn();
                            c.RelativeColumn();
                        });

                        // Header
                        static IContainer HCell(IContainer c) =>
                            c.Background("#1C1A16").PaddingHorizontal(8).PaddingVertical(8);

                        table.Header(h =>
                        {
                            h.Cell().Element(HCell).Text("#").FontColor(Gold).FontSize(9).Bold();
                            h.Cell().Element(HCell).Text("DESCRIPTION").FontColor(Gold).FontSize(9).Bold();
                            h.Cell().Element(HCell).Text("QTY").FontColor(Gold).FontSize(9).Bold().AlignCenter();
                            h.Cell().Element(HCell).Text("UNIT PRICE").FontColor(Gold).FontSize(9).Bold().AlignRight();
                            h.Cell().Element(HCell).Text("AMOUNT").FontColor(Gold).FontSize(9).Bold().AlignRight();
                        });

                        // Rows — CS0172 fix: both branches now return string
                        for (int i = 0; i < d.Items.Count; i++)
                        {
                            var item    = d.Items[i];
                            var rowBg   = i % 2 == 0 ? CreamSoft : White;   // both are string ✓

                            IContainer DCell(IContainer c) =>
                                c.Background(rowBg).PaddingHorizontal(8).PaddingVertical(7)
                                 .BorderBottom(0.3f).BorderColor(BorderGold);

                            table.Cell().Element(DCell)
                                .Text($"{i + 1}").FontColor(WarmGray).FontSize(9);

                            table.Cell().Element(DCell).Column(desc =>
                            {
                                desc.Item().Text(item.Name).Bold();
                                desc.Item().Text(item.Material).FontColor(WarmGray).FontSize(8.5f);
                            });

                            table.Cell().Element(DCell).Text($"{item.Qty}").AlignCenter();
                            table.Cell().Element(DCell).Text($"£{item.UnitPrice:N2}").AlignRight();
                            table.Cell().Element(DCell).Text($"£{item.LineTotal:N2}").AlignRight().Bold();
                        }
                    });

                    // Totals — CS1061 fix: use plain if instead of .If() extension
                    col.Item().PaddingTop(16).AlignRight().Width(220).Column(totals =>
                    {
                        void TotalRow(string label, string value,
                            bool bold = false, string? color = null)
                        {
                            totals.Item().Row(r =>
                            {
                                var lt = r.RelativeItem()
                                    .Text(label)
                                    .FontColor(color ?? WarmGray)
                                    .FontSize(9);
                                if (bold) lt.Bold();       // ← plain if, not .If() ✓

                                var rt = r.ConstantItem(80)
                                    .Text(value)
                                    .AlignRight()
                                    .FontColor(color ?? Charcoal)
                                    .FontSize(9);
                                if (bold) rt.Bold();       // ← plain if, not .If() ✓
                            });
                            totals.Item().PaddingVertical(2)
                                .LineHorizontal(0.3f).LineColor(BorderGold);
                        }

                        TotalRow("Subtotal", $"£{d.Subtotal:N2}");

                        if (d.Discount > 0)
                            TotalRow(
                                $"Discount{(d.PromoCode != null ? $" · Code: {d.PromoCode}" : "")}",
                                $"−£{d.Discount:N2}", color: GreenOk);

                        TotalRow($"Shipping ({d.ShippingTier})",
                            d.Shipping == 0 ? "FREE" : $"£{d.Shipping:N2}");

                        if (d.Vat > 0)
                            TotalRow("VAT (20%)", $"£{d.Vat:N2}");

                        totals.Item().Background(Charcoal).Padding(10).Row(r =>
                        {
                            r.RelativeItem()
                                .Text("TOTAL")
                                .FontColor(CreamSoft).Bold().FontSize(11);
                            r.ConstantItem(90)
                                .Text($"£{d.Total:N2}")
                                .AlignRight().FontColor(Gold).Bold().FontSize(14);
                        });
                    });

                    // Payment note
                    col.Item().PaddingTop(20)
                        .Background(CreamSoft).Border(0.5f).BorderColor(BorderGold)
                        .Padding(12).Column(note =>
                    {
                        note.Item().Text("PAYMENT INFORMATION")
                            .FontSize(8).FontColor(Gold).Bold();
                        note.Item().PaddingTop(4)
                            .Text("Payment for this order will be collected separately. " +
                                  "Please contact us if you have any questions regarding payment or delivery.")
                            .FontSize(9).FontColor(WarmGray).LineHeight(1.6f);
                    });

                    col.Item().PaddingTop(16).AlignCenter()
                        .Text("Thank you for choosing Izale Sparkle")
                        .FontSize(11).FontColor(Gold).Italic();
                });

                // ── FOOTER — CS0023 fix: don't chain .FontSize() on void returns ──
                page.Footer().Column(footer =>
                {
                    footer.Item().LineHorizontal(1).LineColor(Gold);
                    footer.Item().PaddingTop(6).Row(row =>
                    {
                        row.RelativeItem()
                            .Text("Izale Sparkle  ·  45 Ryecroft, Haywards Heath, RH16 4NW  ·  izalesparkle.com")
                            .FontSize(7.5f).FontColor(WarmGray);

                        // CurrentPageNumber() and TotalPages() return void —
                        // wrap in a Text() block and style the surrounding spans instead
                        row.ConstantItem(80).AlignRight().Text(text =>
                        {
                            text.Span("Page ").FontSize(7.5f).FontColor(WarmGray);
                            text.CurrentPageNumber().Style(
                                TextStyle.Default.FontSize(7.5f).FontColor(WarmGray));
                            text.Span(" of ").FontSize(7.5f).FontColor(WarmGray);
                            text.TotalPages().Style(
                                TextStyle.Default.FontSize(7.5f).FontColor(WarmGray));
                        });
                    });
                });
            });
        }).GeneratePdf();
    }
}
