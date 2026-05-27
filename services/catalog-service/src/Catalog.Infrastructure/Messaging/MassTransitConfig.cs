using Catalog.Application.EventHandlers.Integration;
using Catalog.Infrastructure.Persistence;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Catalog.Infrastructure.Messaging;

public static class MassTransitConfig
{
    public static IServiceCollection AddCatalogMessaging(this IServiceCollection services, IConfiguration config, bool registerConsumers)
    {
        var host = config["RabbitMq:Host"] ?? "localhost";
        var user = config["RabbitMq:Username"] ?? "guest";
        var pass = config["RabbitMq:Password"] ?? "guest";

        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();

            x.AddEntityFrameworkOutbox<CatalogDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
                o.QueryDelay = TimeSpan.FromSeconds(1);
            });

            if (registerConsumers)
            {
                x.AddConsumer<WhenOrderPlaced_DecrementStock>();
                x.AddConsumer<WhenOrderCancelled_ReturnStock>();
            }

            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(host, "/", h => { h.Username(user); h.Password(pass); });
                cfg.UseMessageRetry(r => r.Exponential(3,
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromSeconds(2)));
                cfg.ConfigureEndpoints(ctx);
            });
        });
        return services;
    }
}
