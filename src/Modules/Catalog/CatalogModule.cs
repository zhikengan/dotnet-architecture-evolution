using BuildingBlocks.Infrastructure.EventBus;
using BuildingBlocks.Infrastructure.Inbox;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using Catalog.Application.Abstractions;
using Catalog.Application.EventHandlers.Integration;
using Catalog.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orders.Contracts.IntegrationEvents;

namespace Catalog;

public static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services, IConfiguration configuration)
    {
        var assembly = typeof(CatalogModule).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddScoped<DomainEventDispatchInterceptor>();

        services.AddDbContext<CatalogDbContext>((sp, options) =>
        {
            var cs = configuration.GetConnectionString("Marketplace")
                ?? throw new InvalidOperationException("ConnectionStrings:Marketplace required");
            options.UseNpgsql(cs, npg =>
            {
                npg.MigrationsAssembly(typeof(CatalogDbContext).Assembly.FullName);
                npg.MigrationsHistoryTable("__EFMigrationsHistory_Catalog", CatalogDbContext.Schema);
            });
            options.AddInterceptors(sp.GetRequiredService<DomainEventDispatchInterceptor>());
        });

        services.AddScoped<ICatalogDbContext>(sp => sp.GetRequiredService<CatalogDbContext>());
        services.AddScoped<IOutboxStore, CatalogOutboxStore>();
        services.AddScoped<CatalogInboxStore>();

        services.AddScoped<IIntegrationEventHandler<OrderPlacedIntegrationEvent>, WhenOrderPlaced_DecrementStock>();
        services.AddScoped<IIntegrationEventHandler<OrderCancelledIntegrationEvent>, WhenOrderCancelled_ReturnStock>();

        return services;
    }
}
