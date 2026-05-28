using BuildingBlocks.Application;
using BuildingBlocks.Application.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Domain.Orders;
using Orders.Infrastructure.Persistence;
using Quartz;
using Worker.Tests.Fixtures;

namespace Worker.Tests;

[Collection(nameof(WorkerCollection))]
public class ExpireStaleOrdersJobTests(WorkerFixture fx)
{
    [Fact]
    public async Task Job_cancels_Pending_orders_older_than_threshold()
    {
        // Seed a stale Pending order under Acme.
        var staleOrderId = await SeedPendingOrderAsync(WorkerFixture.AcmeTenantId, createdAt: DateTime.UtcNow.AddHours(-1));
        // Seed a fresh Pending order — should be untouched.
        var freshOrderId = await SeedPendingOrderAsync(WorkerFixture.AcmeTenantId, createdAt: DateTime.UtcNow);

        await RunJobAsync();

        using var scope = fx.CreateTenantScope(WorkerFixture.AcmeTenantId);
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        var stale = await db.Orders.AsNoTracking().FirstAsync(o => o.Id == new OrderId(staleOrderId));
        var fresh = await db.Orders.AsNoTracking().FirstAsync(o => o.Id == new OrderId(freshOrderId));
        stale.Status.Should().Be(OrderStatus.Cancelled);
        fresh.Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public async Task Job_walks_all_tenants()
    {
        var acmeOrderId = await SeedPendingOrderAsync(WorkerFixture.AcmeTenantId, createdAt: DateTime.UtcNow.AddHours(-1));
        var globexOrderId = await SeedPendingOrderAsync(WorkerFixture.GlobexTenantId, createdAt: DateTime.UtcNow.AddHours(-1));

        await RunJobAsync();

        using (var acmeScope = fx.CreateTenantScope(WorkerFixture.AcmeTenantId))
        {
            var db = acmeScope.ServiceProvider.GetRequiredService<OrdersDbContext>();
            var o = await db.Orders.AsNoTracking().FirstAsync(o => o.Id == new OrderId(acmeOrderId));
            o.Status.Should().Be(OrderStatus.Cancelled);
        }
        using (var globexScope = fx.CreateTenantScope(WorkerFixture.GlobexTenantId))
        {
            var db = globexScope.ServiceProvider.GetRequiredService<OrdersDbContext>();
            var o = await db.Orders.AsNoTracking().FirstAsync(o => o.Id == new OrderId(globexOrderId));
            o.Status.Should().Be(OrderStatus.Cancelled);
        }
    }

    private async Task<Guid> SeedPendingOrderAsync(Guid tenantId, DateTime createdAt)
    {
        using var scope = fx.CreateTenantScope(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), 1, tenantId, createdAt).Value;
        order.ClearDomainEvents();
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order.Id.Value;
    }

    private async Task RunJobAsync()
    {
        // Invoke the job's Execute directly with a minimal IJobExecutionContext —
        // exercises the production code path without spinning up Quartz's scheduler.
        var scopeFactory = fx.Host.Services.GetRequiredService<IServiceScopeFactory>();
        var clock = fx.Host.Services.GetRequiredService<IClock>();
        var job = new Marketplace.Worker.ScheduledJobs.ExpireStaleOrdersJob(
            scopeFactory, clock, NullLogger<Marketplace.Worker.ScheduledJobs.ExpireStaleOrdersJob>.Instance);
        await job.Execute(new StubJobContext());
    }

    private sealed class StubJobContext : IJobExecutionContext
    {
        public IScheduler Scheduler => null!;
        public ITrigger Trigger => null!;
        public ICalendar? Calendar => null;
        public bool Recovering => false;
        public TriggerKey RecoveringTriggerKey => null!;
        public int RefireCount => 0;
        public JobDataMap MergedJobDataMap => [];
        public IJobDetail JobDetail => null!;
        public IJob JobInstance => null!;
        public DateTimeOffset FireTimeUtc => DateTimeOffset.UtcNow;
        public DateTimeOffset? ScheduledFireTimeUtc => DateTimeOffset.UtcNow;
        public DateTimeOffset? PreviousFireTimeUtc => null;
        public DateTimeOffset? NextFireTimeUtc => null;
        public string FireInstanceId => Guid.NewGuid().ToString();
        public object? Result { get; set; }
        public TimeSpan JobRunTime => TimeSpan.Zero;
        public CancellationToken CancellationToken => CancellationToken.None;
        public void Put(object key, object value) { }
        public object? Get(object key) => null;
    }
}
