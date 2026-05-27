using BuildingBlocks.Application;
using BuildingBlocks.Application.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Orders.Infrastructure.Persistence;
using Platform.Domain.Reporting;
using Platform.Infrastructure.Persistence;
using Quartz;

namespace Marketplace.Worker.ScheduledJobs;

/// <summary>
/// Computes a per-tenant order summary for the previous UTC day and writes
/// one row per tenant into <c>platform.daily_reports</c>. Idempotent — if the
/// row already exists for that (tenant, date) it skips.
/// </summary>
[DisallowConcurrentExecution]
public sealed class DailyReportingJob(
    IServiceScopeFactory scopeFactory,
    IClock clock,
    ILogger<DailyReportingJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var now = clock.UtcNow;
        var reportDate = DateOnly.FromDateTime(now.AddDays(-1));
        var rangeStart = reportDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var rangeEnd = rangeStart.AddDays(1);

        using var rootScope = scopeFactory.CreateScope();
        var platform = rootScope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var tenants = await platform.Tenants.AsNoTracking().Select(t => t.Id).ToListAsync(context.CancellationToken);

        foreach (var tenantId in tenants)
        {
            using var scope = scopeFactory.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContextSetter>().SetTenant(tenantId);
            var orders = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
            var platformScoped = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

            var exists = await platformScoped.DailyReports
                .IgnoreQueryFilters()
                .AnyAsync(r => r.TenantId == tenantId && r.Date == reportDate, context.CancellationToken);
            if (exists) continue;

            var window = await orders.Orders
                .Where(o => o.CreatedAt >= rangeStart && o.CreatedAt < rangeEnd)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Confirmed = g.Count(o => o.Status == global::Orders.Domain.Orders.OrderStatus.Confirmed),
                    Cancelled = g.Count(o => o.Status == global::Orders.Domain.Orders.OrderStatus.Cancelled),
                })
                .FirstOrDefaultAsync(context.CancellationToken);

            // Revenue requires joining order × product price; at this scope the
            // Catalog price isn't trivially reachable, so total revenue is a
            // placeholder (0) until the Reporting module owns its own snapshot.
            var report = DailyReport.Create(
                tenantId, reportDate,
                window?.Total ?? 0, window?.Confirmed ?? 0, window?.Cancelled ?? 0,
                revenue: 0m,
                now);

            platformScoped.DailyReports.Add(report);
            await platformScoped.SaveChangesAsync(context.CancellationToken);
            logger.LogInformation("DailyReportingJob: wrote report for tenant {TenantId} date {Date}", tenantId, reportDate);
        }
    }
}
