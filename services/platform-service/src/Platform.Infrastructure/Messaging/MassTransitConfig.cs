using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Persistence;

namespace Platform.Infrastructure.Messaging;

public static class MassTransitConfig
{
    public static IServiceCollection AddPlatformMessaging(this IServiceCollection services, IConfiguration config)
    {
        var host = config["RabbitMq:Host"] ?? "localhost";
        var user = config["RabbitMq:Username"] ?? "guest";
        var pass = config["RabbitMq:Password"] ?? "guest";

        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();
            x.AddEntityFrameworkOutbox<PlatformDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
                o.QueryDelay = TimeSpan.FromSeconds(1);
            });
            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(host, "/", h => { h.Username(user); h.Password(pass); });
                cfg.UseMessageRetry(r => r.Exponential(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2)));
                cfg.ConfigureEndpoints(ctx);
            });
        });
        return services;
    }
}
