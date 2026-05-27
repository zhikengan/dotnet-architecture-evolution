using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Application.Consumers;
using Notifications.Infrastructure.Persistence;

namespace Notifications.Infrastructure.Messaging;

public static class MassTransitConfig
{
    public static IServiceCollection AddNotificationsMessaging(this IServiceCollection services, IConfiguration config, bool registerConsumers)
    {
        var host = config["RabbitMq:Host"] ?? "localhost";
        var user = config["RabbitMq:Username"] ?? "guest";
        var pass = config["RabbitMq:Password"] ?? "guest";

        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();

            x.AddEntityFrameworkOutbox<NotificationsDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
                o.QueryDelay = TimeSpan.FromSeconds(1);
            });

            if (registerConsumers)
            {
                x.AddConsumer<WhenOrderConfirmedConsumer>();
                x.AddConsumer<WhenOrderCancelledConsumer>();
                x.AddConsumer<WhenOrderFailedConsumer>();
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
