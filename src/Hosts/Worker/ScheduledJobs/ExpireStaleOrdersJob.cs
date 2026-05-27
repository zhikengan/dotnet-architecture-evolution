using BuildingBlocks.Application;
using BuildingBlocks.Application.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Orders.Application.Orders.ForceCancelOrder;
using Orders.Infrastructure.Persistence;
using Quartz;

namespace Marketplace.Worker.ScheduledJobs;

/// <summary>
/// Cancels orders stuck in <c>Pending</c> beyond the staleness threshold.
/// Walks every tenant — Worker scope has no ambient tenant, so the job
/// iterates Tenants explicitly and sets the context per tenant before
/// touching the orders DbContext (otherwise the query filter sees nothing).
/// </summary>
[DisallowConcurrentExecution]
public sealed class ExpireStaleOrdersJob(
    IServiceScopeFactory scopeFactory,
    IClock clock,
    ILogger<ExpireStaleOrdersJob> logger) : IJob
{
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(30);

    public async Task Execute(IJobExecutionContext context)
    {
        using var rootScope = scopeFactory.CreateScope();
        var platformDb = rootScope.ServiceProvider.GetRequiredService<global::Platform.Infrastructure.Persistence.PlatformDbContext>();
        var tenants = await platformDb.Tenants.AsNoTracking().Select(t => t.Id).ToListAsync(context.CancellationToken);

        var cutoff = clock.UtcNow - StaleThreshold;
        foreach (var tenantId in tenants)
        {
            using var tenantScope = scopeFactory.CreateScope();
            tenantScope.ServiceProvider.GetRequiredService<ITenantContextSetter>().SetTenant(tenantId);
            var ordersDb = tenantScope.ServiceProvider.GetRequiredService<OrdersDbContext>();

            var stale = await ordersDb.Orders
                .Where(o => o.Status == global::Orders.Domain.Orders.OrderStatus.Pending && o.CreatedAt < cutoff)
                .Select(o => o.Id.Value)
                .ToListAsync(context.CancellationToken);

            if (stale.Count == 0) continue;

            var mediator = tenantScope.ServiceProvider.GetRequiredService<MediatR.ISender>();
            foreach (var id in stale)
            {
                var r = await mediator.Send(new ForceCancelOrderCommand(id), context.CancellationToken);
                if (r.IsFailure)
                    logger.LogWarning("ExpireStaleOrdersJob: cancel of {OrderId} (tenant {TenantId}) failed: {Error}", id, tenantId, r.Error.Code);
            }
            logger.LogInformation("ExpireStaleOrdersJob: cancelled {Count} stale orders for tenant {TenantId}", stale.Count, tenantId);
        }
    }
}
