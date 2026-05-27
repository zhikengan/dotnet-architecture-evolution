using Marketplace.Application.Abstractions;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Infrastructure.Persistence.Interceptors;
using Marketplace.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<DomainEventDispatchInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var cs = configuration.GetConnectionString("Default")
                ?? throw new InvalidOperationException("ConnectionStrings:Default is required");
            options.UseNpgsql(cs, npg => npg.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
            options.AddInterceptors(sp.GetRequiredService<DomainEventDispatchInterceptor>());
        });

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IClock, SystemClock>();

        return services;
    }
}
