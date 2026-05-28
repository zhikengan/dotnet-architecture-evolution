using BuildingBlocks.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BuildingBlocks.Api.HealthChecks;

/// <summary>
/// Surfaces "outbox is stuck" the way an operator would: query the oldest
/// unprocessed row across every module's outbox, compare its <c>OccurredAt</c>
/// to now. Degraded if the lag crosses 30s, unhealthy past 5 minutes — the
/// numbers from <c>runbooks/outbox-stuck.md</c>.
/// </summary>
public sealed class OutboxLagHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    private static readonly TimeSpan DegradedAt = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan UnhealthyAt = TimeSpan.FromMinutes(5);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var stores = scope.ServiceProvider.GetServices<IOutboxStore>().ToList();
        TimeSpan worst = TimeSpan.Zero;
        string? worstModule = null;

        foreach (var store in stores)
        {
            var pending = await store.GetPendingAsync(1, cancellationToken);
            if (pending.Count == 0) continue;
            var lag = DateTime.UtcNow - pending[0].OccurredAt;
            if (lag > worst) { worst = lag; worstModule = store.ModuleName; }
        }

        if (worst >= UnhealthyAt)
            return HealthCheckResult.Unhealthy($"Outbox lag {worst.TotalSeconds:F0}s on {worstModule} exceeds {UnhealthyAt.TotalSeconds:F0}s");
        if (worst >= DegradedAt)
            return HealthCheckResult.Degraded($"Outbox lag {worst.TotalSeconds:F0}s on {worstModule} exceeds {DegradedAt.TotalSeconds:F0}s");
        return HealthCheckResult.Healthy($"max_lag_seconds={worst.TotalSeconds:F0}");
    }
}
