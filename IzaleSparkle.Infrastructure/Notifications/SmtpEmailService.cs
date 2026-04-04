using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using IzaleSparkle.Application.Common.Interfaces;

namespace IzaleSparkle.Infrastructure.Notifications;

public class SmtpEmailService(IConfiguration config, ILogger<SmtpEmailService> log)
    : IEmailService
{
    // ── ORDER CONFIRMATION + INVOICE PDF ─────────────────────────
    public async Task SendOrderConfirmationAsync(OrderEmailData data, CancellationToken ct = default)
    {
        var subject = $"✦ Order Confirmed — {data.OrderNumber} | Izale Sparkle";
        var html    = BuildOrderConfirmationHtml(data);

        // Generate PDF invoice and attach it
        byte[]? pdfBytes = null;
        try
        {
            pdfBytes = InvoiceGenerator.Generate(data);
            log.LogInformation("[Invoice] PDF generated for {OrderNumber} ({Size} bytes)",
                data.OrderNumber, pdfBytes.Length);
        }
        catch (Exception ex)
        {
            // PDF generation failure must never block the confirmation email
            log.LogError(ex, "[Invoice] PDF generation failed for {OrderNumber}", data.OrderNumber);
        }

        var invoiceFilename = $"Izale-Sparkle-Invoice-{data.OrderNumber}.pdf";

        // 1. Send confirmation + invoice to customer
        await SendAsync(data.CustomerEmail, data.CustomerName, subject, html,
            ct, pdfBytes, invoiceFilename);
        log.LogInformation("[Email] Order confirmation sent → {Email} | {OrderNumber}",
            data.CustomerEmail, data.OrderNumber);

        // 2. Send internal order details to admin (replaces WhatsApp)
        var adminEmail = config["Email:AdminAddress"];
        if (!string.IsNullOrEmpty(adminEmail))
        {
            var adminSubject = $"[NEW ORDER] {data.OrderNumber} — £{data.Total:N2} — {data.CustomerName}";
            var adminHtml    = BuildAdminOrderHtml(data);
            await SendAsync(adminEmail, "Izale Sparkle Admin", adminSubject,
                adminHtml, ct, pdfBytes, invoiceFilename);
            log.LogInformation("[Email] Admin order notification sent → {Admin} | {OrderNumber}",
                adminEmail, data.OrderNumber);
        }
    }



    // ── ORDER STATUS UPDATE ───────────────────────────────────────
    public async Task SendOrderStatusUpdateAsync(
        string customerEmail, string customerName, string orderNumber,
        string newStatus, string? trackingNumber, CancellationToken ct = default)
    {
        var (emoji, title, body) = newStatus switch
        {
            "PaymentReceived" => ("💳", "Payment Received",
                "Great news! We have received your payment and your order is now being prepared."),
            "Processing" => ("⚙️", "Order Being Prepared",
                "Your jewellery is now being carefully prepared and quality-checked by our craftsmen."),
            "Shipped" => ("🚚", "Your Order Is On Its Way",
                trackingNumber != null
                    ? $"Your order has been dispatched. Your tracking number is <strong>{System.Net.WebUtility.HtmlEncode(trackingNumber)}</strong>. You can use this to track your delivery."
                    : "Your order has been dispatched and is on its way to you."),
            "Delivered" => ("✅", "Order Delivered",
                "Your order has been delivered. We hope you love your new Izale Sparkle piece!"),
            "Cancelled" => ("❌", "Order Cancelled",
                "Your order has been cancelled. If you have any questions, please contact us."),
            "Refunded" => ("↩️", "Order Refunded",
                "Your refund has been processed. Please allow 3–5 business days for it to appear."),
            _ => ("📦", $"Order Status: {newStatus}",
                $"Your order status has been updated to <strong>{System.Net.WebUtility.HtmlEncode(newStatus)}</strong>.")
        };

        var trackingBlock = (newStatus == "Shipped" && !string.IsNullOrEmpty(trackingNumber))
            ? $"""
              <div style="margin:20px 0;background:#faf7ee;border:1px solid #e8dfc4;padding:16px 20px;text-align:center">
                <div style="font-size:11px;letter-spacing:3px;text-transform:uppercase;color:#C8973A;margin-bottom:6px">Tracking Number</div>
                <div style="font-size:20px;font-family:monospace;font-weight:700;color:#1C1A16;letter-spacing:.1em">{System.Net.WebUtility.HtmlEncode(trackingNumber)}</div>
              </div>
              """
            : "";

        var html = $"""
<!DOCTYPE html><html><head><meta charset="utf-8"/></head>
<body style="margin:0;padding:0;background:#f5f0e1;font-family:'Georgia',serif">
<table width="100%" cellpadding="0" cellspacing="0" style="padding:30px 20px">
<tr><td align="center">
<table width="600" style="background:#fff;max-width:600px;width:100%">
  <tr><td style="background:#1C1A16;padding:28px 40px;text-align:center">
    <h1 style="margin:0;color:#C8973A;font-weight:400;font-size:24px;letter-spacing:.25em">Izale ✦ Sparkle</h1>
  </td></tr>
  <tr><td style="background:#C8973A;padding:16px 40px;text-align:center">
    <span style="font-size:28px">{emoji}</span>
    <h2 style="margin:4px 0 0;color:#1C1A16;font-weight:400;font-size:18px">{title}</h2>
  </td></tr>
  <tr><td style="padding:32px 40px">
    <p style="color:#7A6E5F;font-size:14px">Dear {System.Net.WebUtility.HtmlEncode(customerName)},</p>
    <p style="color:#1C1A16;font-size:15px;line-height:1.7">{body}</p>
    {trackingBlock}
    <div style="background:#faf7ee;border:1px solid #e8dfc4;padding:14px 18px;margin:20px 0">
      <div style="font-size:11px;letter-spacing:3px;text-transform:uppercase;color:#C8973A;margin-bottom:4px">Order Number</div>
      <div style="font-size:18px;color:#1C1A16;letter-spacing:.1em">{System.Net.WebUtility.HtmlEncode(orderNumber)}</div>
    </div>
    <p style="color:#7A6E5F;font-size:13px;line-height:1.7">
      If you have any questions, please reply to this email or visit our
      <a href="https://izalesparkle.com/contact" style="color:#C8973A">contact page</a>.
    </p>
    <p style="font-style:italic;color:#C8973A;font-size:15px;margin-top:24px">Where Every Look Sparkles. ✦</p>
  </td></tr>
  <tr><td style="background:#1C1A16;padding:20px 40px;text-align:center">
    <p style="margin:0;color:rgba(245,240,225,.3);font-size:11px">© 2024 Izale Sparkle · 24 Hatton Garden, London EC1N 8DB</p>
  </td></tr>
</table></td></tr></table>
</body></html>
""";

        var subject = $"{emoji} Order {orderNumber} — {title}";
        await SendAsync(customerEmail, customerName, subject, html, ct);
        log.LogInformation("[Email] Status update sent → {Email} | {OrderNumber} | {Status}",
            customerEmail, orderNumber, newStatus);
    }

    // ── ADMIN ORDER NOTIFICATION ──────────────────────────────────
    public async Task SendAdminOrderNotificationAsync(OrderEmailData data, CancellationToken ct = default)
    {
        var adminEmail = config["Email:AdminAddress"] ?? config["Email:From"] ?? "";
        if (string.IsNullOrEmpty(adminEmail)) return;

        var subject = $"🔔 New Order — {data.OrderNumber} | £{data.Total:N2}";

        var itemRows = string.Join("", data.Items.Select(i =>
            $"<tr><td style='padding:6px 0;border-bottom:1px solid #f0ead8;color:#1C1A16'><strong>{System.Net.WebUtility.HtmlEncode(i.Name)}</strong><br/><small style='color:#7A6E5F'>{System.Net.WebUtility.HtmlEncode(i.Material)} · Qty {i.Qty}</small></td>" +
            $"<td style='padding:6px 0;border-bottom:1px solid #f0ead8;text-align:right;color:#C8973A;font-family:Georgia,serif'>£{i.LineTotal:N2}</td></tr>"));

        var discountRow = data.Discount > 0
            ? $"<tr><td style='color:#16a34a'>Discount ({System.Net.WebUtility.HtmlEncode(data.PromoCode ?? "")})</td><td style='text-align:right;color:#16a34a'>−£{data.Discount:N2}</td></tr>"
            : "";

        var addrHtml = string.Join("<br/>", data.ShippingAddress
            .Split('\n', System.StringSplitOptions.RemoveEmptyEntries)
            .Select(System.Net.WebUtility.HtmlEncode));

        var html = $"""
<div style="font-family:Arial,sans-serif;max-width:640px;margin:0 auto;background:#FAF7EE;padding:24px">
  <div style="background:#1C1A16;padding:16px 24px;margin-bottom:0">
    <span style="color:#C8973A;font-size:18px;font-family:Georgia,serif">Izale ✦ Sparkle</span>
    <span style="float:right;background:#C8973A;color:#1C1A16;padding:4px 12px;font-size:12px;font-weight:bold">NEW ORDER</span>
  </div>
  <div style="background:white;padding:24px;border:1px solid #e8dfc4">
    <table width="100%" style="margin-bottom:16px">
      <tr>
        <td><span style="font-size:11px;color:#C8973A;letter-spacing:2px;text-transform:uppercase">Order Number</span><br/><strong style="font-size:16px">{data.OrderNumber}</strong></td>
        <td style="text-align:right"><span style="font-size:11px;color:#C8973A;letter-spacing:2px;text-transform:uppercase">Total</span><br/><strong style="font-size:20px;color:#C8973A;font-family:Georgia,serif">£{data.Total:N2}</strong></td>
      </tr>
    </table>
    <hr style="border:none;border-top:1px solid #e8dfc4;margin:12px 0"/>
    <p style="margin:0 0 4px;font-size:11px;color:#C8973A;letter-spacing:2px;text-transform:uppercase">Customer</p>
    <p style="margin:0 0 16px;color:#1C1A16"><strong>{System.Net.WebUtility.HtmlEncode(data.CustomerName)}</strong> · {System.Net.WebUtility.HtmlEncode(data.CustomerEmail)}</p>
    <p style="margin:0 0 4px;font-size:11px;color:#C8973A;letter-spacing:2px;text-transform:uppercase">Items to Pack</p>
    <table width="100%" style="margin-bottom:16px">{itemRows}</table>
    <table width="100%" style="font-size:13px;margin-bottom:16px">
      <tr><td style="color:#7A6E5F">Subtotal</td><td style="text-align:right">£{data.Subtotal:N2}</td></tr>
      {discountRow}
      <tr><td style="color:#7A6E5F">Shipping ({System.Net.WebUtility.HtmlEncode(data.ShippingTier)})</td><td style="text-align:right">{(data.Shipping == 0 ? "<span style='color:#16a34a'>FREE</span>" : $"£{data.Shipping:N2}")}</td></tr>
      <tr><td style="color:#7A6E5F">VAT (20%)</td><td style="text-align:right">£{data.Vat:N2}</td></tr>
      <tr style="border-top:2px solid #C8973A"><td style="padding-top:8px"><strong>Total</strong></td><td style="text-align:right;padding-top:8px"><strong style="font-size:16px;color:#C8973A">£{data.Total:N2}</strong></td></tr>
    </table>
    <hr style="border:none;border-top:1px solid #e8dfc4;margin:12px 0"/>
    <p style="margin:0 0 4px;font-size:11px;color:#C8973A;letter-spacing:2px;text-transform:uppercase">Deliver To</p>
    <p style="margin:0;color:#1C1A16;line-height:1.8">{addrHtml}</p>
    <p style="margin:16px 0 0;background:#fff8e6;border:1px solid #e8dfc4;padding:10px 14px;font-size:12px;color:#7A6E5F">
      ✅ Payment will be collected separately. This order needs to be packed and dispatched.
    </p>
  </div>
</div>
""";

        await SendAsync(adminEmail, "Izale Sparkle Admin", subject, html, ct);
        log.LogInformation("[Email] Admin order notification sent for {OrderNumber}", data.OrderNumber);
    }

    // ── CONTACT FORM ──────────────────────────────────────────────
    public async Task SendContactEmailAsync(
        string from, string name, string subject, string body, CancellationToken ct = default)
    {
        var html = $"""
            <div style="font-family:sans-serif;max-width:600px;margin:0 auto">
              <h2 style="color:#1C1A16">New Contact Enquiry</h2>
              <p><strong>From:</strong> {System.Net.WebUtility.HtmlEncode(name)} ({System.Net.WebUtility.HtmlEncode(from)})</p>
              <p><strong>Subject:</strong> {System.Net.WebUtility.HtmlEncode(subject)}</p>
              <hr style="border-color:#C8973A" />
              <p style="white-space:pre-wrap">{System.Net.WebUtility.HtmlEncode(body)}</p>
            </div>
            """;

        var adminEmail = config["Email:AdminAddress"] ?? config["Email:From"] ?? "";
        if (!string.IsNullOrEmpty(adminEmail))
            await SendAsync(adminEmail, "Izale Sparkle Admin", $"Contact: {subject}", html, ct);
    }

    // ── NEWSLETTER WELCOME ────────────────────────────────────────
    public async Task SendNewsletterWelcomeAsync(string email, CancellationToken ct = default)
    {
        var html = """
            <div style="font-family:'Georgia',serif;max-width:600px;margin:0 auto;background:#FAF7EE;padding:40px">
              <h1 style="color:#C8973A;font-weight:400;letter-spacing:0.2em">Izale ✦ Sparkle</h1>
              <h2 style="color:#1C1A16;font-weight:400">Welcome to the Sparkle Circle</h2>
              <p style="color:#7A6E5F;line-height:1.8">
                You're now part of our inner circle — the first to know about new arrivals,
                exclusive events and jewellery care tips.
              </p>
              <p style="color:#7A6E5F;line-height:1.8;font-style:italic">
                Where Every Look Sparkles. ✦
              </p>
            </div>
            """;
        await SendAsync(email, "Sparkle Circle Member", "Welcome to Izale Sparkle ✦", html, ct);
    }

    // ── GENERATE PDF (for admin download) ────────────────────────
    public Task<byte[]> GenerateInvoicePdfAsync(OrderEmailData data, CancellationToken ct = default)
    {
        var bytes = InvoiceGenerator.Generate(data);
        return Task.FromResult(bytes);
    }

    // ── CORE SEND ─────────────────────────────────────────────────
    async Task SendAsync(
        string toEmail, string toName, string subject, string htmlBody,
        CancellationToken ct,
        byte[]? attachmentBytes = null, string? attachmentFilename = null)
    {
        var smtpHost = config["Email:SmtpHost"];
        if (string.IsNullOrEmpty(smtpHost))
        {
            log.LogWarning("[Email] SMTP not configured — skipping send to {Email}", toEmail);
            return;
        }

        var port     = int.TryParse(config["Email:SmtpPort"], out var p) ? p : 587;
        var user     = config["Email:Username"]  ?? "";
        var pass     = config["Email:Password"]  ?? "";
        var fromAddr = config["Email:From"]      ?? user;
        var fromName = config["Email:FromName"]  ?? "Izale Sparkle";

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromAddr));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = subject;

        // Build multipart body when there is an attachment
        if (attachmentBytes != null && !string.IsNullOrEmpty(attachmentFilename))
        {
            var multipart = new Multipart("mixed");

            // HTML body part
            multipart.Add(new TextPart("html") { Text = htmlBody });

            // PDF attachment part
            var attachment = new MimePart("application", "pdf")
            {
                Content            = new MimeContent(new MemoryStream(attachmentBytes)),
                ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                ContentTransferEncoding = ContentEncoding.Base64,
                FileName           = attachmentFilename,
            };
            multipart.Add(attachment);
            message.Body = multipart;
        }
        else
        {
            message.Body = new TextPart("html") { Text = htmlBody };
        }

        using var client = new SmtpClient();
        await client.ConnectAsync(smtpHost, port, SecureSocketOptions.StartTls, ct);
        await client.AuthenticateAsync(user, pass, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }

}
