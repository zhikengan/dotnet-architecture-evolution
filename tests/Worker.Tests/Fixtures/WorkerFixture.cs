using BuildingBlocks.Application.MultiTenancy;
using BuildingBlocks.Infrastructure.MultiTenancy;
using Catalog;
using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orders;
using Orders.Infrastructure.Persistence;
using Platform;
using Platform.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Worker.Tests.Fixtures;

/// <summary>
/// Spins up a real IHost with all three modules + the OutboxProcessor against
/// a Testcontainers Postgres. Uses <see cref="Host.CreateApplicationBuilder"/>
/// rather than WebApplicationFactory because the Worker is an IHost, not a
/// WebApplication — these tests deliberately exercise the no-HTTP shape.
/// </summary>
public sealed class WorkerFixture : IAsyncLifetime
{
    public static readonly Guid AcmeTenantId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid GlobexTenantId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("marketplace_worker_tests")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public IHost Host { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Marketplace"] = _container.GetConnectionString(),
            ["Outbox:PollIntervalMilliseconds"] = "200",
            ["Outbox:BatchSize"] = "50",
            ["Outbox:MaxRetries"] = "3",
            ["FeatureFlags:CacheSeconds"] = "1",
        });

        builder.Services.AddSingleton<BuildingBlocks.Application.IClock, BuildingBlocks.Infrastructure.Time.SystemClock>();
        builder.Services.AddSingleton<BuildingBlocks.Infrastructure.EventBus.IEventBus, BuildingBlocks.Infrastructure.EventBus.InMemoryEventBus>();

        builder.Services.AddScoped<TenantContext>();
        builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        builder.Services.AddScoped<ITenantContextSetter>(sp => sp.GetRequiredService<TenantContext>());

        builder.Services.AddPlatformModule(builder.Configuration);
        builder.Services.AddCatalogModule(builder.Configuration);
        builder.Services.AddOrdersModule(builder.Configuration);

        builder.Services.Configure<BuildingBlocks.Infrastructure.Outbox.OutboxOptions>(
            builder.Configuration.GetSection(BuildingBlocks.Infrastructure.Outbox.OutboxOptions.SectionName));
        builder.Services.AddHostedService<BuildingBlocks.Infrastructure.Outbox.OutboxProcessor>();

        Host = builder.Build();

        // Migrate + seed up-front so tests can immediately exercise jobs.
        using var scope = Host.Services.CreateScope();
        var pdb = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var cdb = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var odb = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        await pdb.Database.MigrateAsync();
        await cdb.Database.MigrateAsync();
        await odb.Database.MigrateAsync();
        await PlatformDataSeeder.SeedAsync(pdb);
        await CatalogDataSeeder.SeedAsync(cdb);
    }

    public async Task DisposeAsync()
    {
        Host?.Dispose();
        await _container.DisposeAsync();
    }

    public IServiceScope CreateTenantScope(Guid tenantId)
    {
        var scope = Host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextSetter>().SetTenant(tenantId);
        return scope;
    }
}

[CollectionDefinition(nameof(WorkerCollection))]
public class WorkerCollection : ICollectionFixture<WorkerFixture>;
