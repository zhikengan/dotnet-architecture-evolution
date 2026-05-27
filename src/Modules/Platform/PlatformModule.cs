using BuildingBlocks.Application;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Application.Abstractions;
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
        services.AddSingleton<IFeatureFlagQuery, DbFeatureManager>();

        services.AddScoped<IIdempotencyStore, PlatformIdempotencyStore>();

        return services;
    }
}
