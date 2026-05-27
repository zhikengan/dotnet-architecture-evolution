using BuildingBlocks.Infrastructure.Inbox;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orders.Application.Abstractions;
using Orders.Infrastructure.Persistence;

namespace Orders;

public static class OrdersModule
{
    public static IServiceCollection AddOrdersModule(this IServiceCollection services, IConfiguration configuration)
    {
        var assembly = typeof(OrdersModule).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddScoped<DomainEventDispatchInterceptor>();

        services.AddDbContext<OrdersDbContext>((sp, options) =>
        {
            var cs = configuration.GetConnectionString("Marketplace")
                ?? throw new InvalidOperationException("ConnectionStrings:Marketplace required");
            options.UseNpgsql(cs, npg =>
            {
                npg.MigrationsAssembly(typeof(OrdersDbContext).Assembly.FullName);
                npg.MigrationsHistoryTable("__EFMigrationsHistory_Orders", OrdersDbContext.Schema);
            });
            options.AddInterceptors(sp.GetRequiredService<DomainEventDispatchInterceptor>());
        });

        services.AddScoped<IOrdersDbContext>(sp => sp.GetRequiredService<OrdersDbContext>());
        services.AddScoped<IOutboxStore, OrdersOutboxStore>();
        services.AddScoped<OrdersInboxStore>();

        return services;
    }
}
