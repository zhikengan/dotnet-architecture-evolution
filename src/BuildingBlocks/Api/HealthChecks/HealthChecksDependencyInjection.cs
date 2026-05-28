using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BuildingBlocks.Api.HealthChecks;

/// <summary>
/// Wires the three readiness checks: Postgres connectivity, MinIO bucket
/// reachability, and outbox-lag. Liveness is the host process answering at
/// all and is mounted directly in Program.cs (no checks behind it).
/// </summary>
public static class HealthChecksDependencyInjection
{
    public const string ReadyTag = "ready";
    public const string LiveTag = "live";

    public static IServiceCollection AddMarketplaceHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Marketplace")
            ?? throw new InvalidOperationException("ConnectionStrings:Marketplace required for health checks");

        services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgres", tags: [ReadyTag])
            .AddCheck<MinioHealthCheck>("minio", tags: [ReadyTag])
            .AddCheck<OutboxLagHealthCheck>("outbox-lag", tags: [ReadyTag]);

        return services;
    }
}
