using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Infrastructure.Storage;

/// <summary>
/// Picks the <see cref="IFileStorage"/> impl from config — "S3" for MinIO /
/// real S3, anything else (or empty) for the local-filesystem fallback used
/// in unit tests. Hosts call this once.
/// </summary>
public static class StorageDependencyInjection
{
    public static IServiceCollection AddMarketplaceStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var provider = configuration.GetSection(StorageOptions.SectionName)["Provider"] ?? "Local";
        if (string.Equals(provider, "S3", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IFileStorage, S3FileStorage>();
        }
        else
        {
            services.AddSingleton<IFileStorage, LocalFileStorage>();
        }
        return services;
    }
}
