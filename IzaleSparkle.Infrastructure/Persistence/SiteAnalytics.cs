using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IzaleSparkle.Application.Common.Interfaces;
using IzaleSparkle.Domain.Entities;

namespace IzaleSparkle.Infrastructure.Persistence;

/// <summary>
/// EF Core backed website-visit tracker. One row per visit; counts are computed
/// on read. Recording never throws so a logging failure can't break page loads.
/// </summary>
public class SiteAnalytics(AppDbContext db, ILogger<SiteAnalytics> log) : ISiteAnalytics
{
    public async Task RecordVisitAsync(string? path, CancellationToken ct = default)
    {
        try
        {
            if (path is { Length: > 300 }) path = path[..300];
            db.SiteVisits.Add(new SiteVisit { Path = path });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "[Analytics] Failed to record site visit");
        }
    }

    public async Task<(int Total, int Today)> GetViewCountsAsync(CancellationToken ct = default)
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            var total = await db.SiteVisits.CountAsync(ct);
            var todayCount = await db.SiteVisits.CountAsync(v => v.CreatedAt >= today, ct);
            return (total, todayCount);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "[Analytics] Failed to read site view counts");
            return (0, 0);
        }
    }
}
