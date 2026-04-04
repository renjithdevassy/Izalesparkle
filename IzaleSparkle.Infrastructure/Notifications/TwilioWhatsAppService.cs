using Microsoft.Extensions.Logging;
using IzaleSparkle.Application.Common.Interfaces;

namespace IzaleSparkle.Infrastructure.Notifications;

/// <summary>
/// WhatsApp notifications are disabled.
/// All IWhatsAppService calls are no-ops — order details are sent via email instead.
/// </summary>
public class MetaWhatsAppService(ILogger<MetaWhatsAppService> log) : IWhatsAppService
{
    public Task SendOrderNotificationAsync(OrderEmailData data, CancellationToken ct = default)
    {
        log.LogInformation("[WhatsApp] Disabled — order {OrderNumber} notified via email.", data.OrderNumber);
        return Task.CompletedTask;
    }

    public Task SendContactNotificationAsync(string fromName, string email, string message, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task SendLowStockAlertAsync(string productName, int stockLevel, CancellationToken ct = default)
        => Task.CompletedTask;
}
