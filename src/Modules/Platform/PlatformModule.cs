using BuildingBlocks.Application;
using FluentValidation;
using Hangfire;
using Hangfire.PostgreSql;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Application.Abstractions;
using Platform.Application.Emails;
using Platform.Application.EventHandlers.Integration;
using Platform.Contracts;
using Platform.Infrastructure.FeatureManagement;
using Platform.Infrastructure.Persistence;

namespace Platform;

public static class PlatformModule
{
    public static IServiceCollection AddPlatformModule(this IServiceCollection services, IConfiguration configuration)
    {
        var assembly = typeof(PlatformModule).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddDbContext<PlatformDbContext>((sp, options) =>
        {
            var cs = configuration.GetConnectionString("Marketplace")
                ?? throw new InvalidOperationException("ConnectionStrings:Marketplace required");
            options.UseNpgsql(cs, npg =>
            {
                npg.MigrationsAssembly(typeof(PlatformDbContext).Assembly.FullName);
                npg.MigrationsHistoryTable("__EFMigrationsHistory_Platform", PlatformDbContext.Schema);
            });
        });

        services.AddScoped<IPlatformDbContext>(sp => sp.GetRequiredService<PlatformDbContext>());

        services.Configure<PlatformOptions>(configuration.GetSection(PlatformOptions.SectionName));
        services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PlatformOptions>>().Value);

        services.AddMemoryCache();
        // Scoped — needs the request's ITenantContext + PlatformDbContext so
        // tenant query filters and cache keys resolve to the right tenant.
        services.AddScoped<IFeatureFlagQuery, DbFeatureManager>();

        services.AddScoped<IIdempotencyStore, PlatformIdempotencyStore>();

        // Hangfire — client + Postgres storage. Both API and Worker register
        // this so either side can enqueue jobs; only the Worker adds the
        // server. The connection string is resolved lazily from IConfiguration
        // via the (sp, cfg) overload so WebApplicationFactory-style test
        // overrides — which only land in config AFTER module DI runs — are
        // still picked up at first use, matching the same lazy pattern the
        // EF DbContext registrations use.
        services.AddHangfire((sp, cfg) =>
        {
            var liveConfig = sp.GetRequiredService<IConfiguration>();
            var marketplaceConnection = liveConfig.GetConnectionString("Marketplace")
                ?? throw new InvalidOperationException("ConnectionStrings:Marketplace required for Hangfire");
            cfg.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(opts => opts.UseNpgsqlConnection(marketplaceConnection));
        });

        services.AddScoped<SendOrderEmailService>();
        services.AddScoped<BuildingBlocks.Infrastructure.EventBus.IIntegrationEventHandler<Orders.Contracts.IntegrationEvents.OrderConfirmedIntegrationEvent>, WhenOrderConfirmed_SendEmail>();

        return services;
    }
}
