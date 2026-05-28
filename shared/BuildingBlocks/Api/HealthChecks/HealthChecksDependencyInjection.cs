using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace BuildingBlocks.Api.HealthChecks;

/// <summary>
/// Standard liveness/readiness probes for every service:
///   <c>GET /health/live</c> — process is responsive (no dependency checks).
///   <c>GET /health/ready</c> — Postgres + RabbitMQ reachable; safe to receive traffic.
/// Mounted before auth so probes don't require a token. Designed for K8s
/// <c>livenessProbe</c> / <c>readinessProbe</c> compatibility.
/// </summary>
public static class HealthChecksDependencyInjection
{
    public const string ReadyTag = "ready";
    public const string LiveTag = "live";

    /// <summary>
    /// Registers Postgres + RabbitMQ readiness checks. Services pass the same
    /// connection string they hand to <c>UseNpgsql</c> so probes use the same
    /// credentials EF does.
    /// </summary>
    public static IServiceCollection AddMarketplaceHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration,
        string postgresConnectionString)
    {
        var rabbitHost = configuration["RabbitMq:Host"] ?? "localhost";
        var rabbitUser = configuration["RabbitMq:Username"] ?? "guest";
        var rabbitPass = configuration["RabbitMq:Password"] ?? "guest";
        var rabbitUri = $"amqp://{rabbitUser}:{rabbitPass}@{rabbitHost}:5672/";

        var builder = services.AddHealthChecks();

        if (!string.IsNullOrEmpty(postgresConnectionString))
        {
            builder.AddNpgSql(postgresConnectionString, name: "postgres", tags: [ReadyTag]);
        }

        // RabbitMQ.Client 7 is async-first; the AspNetCore.HealthChecks.RabbitMQ
        // overload takes a Func<IServiceProvider, Task<IConnection>>. ConnectionFactory
        // is constructed once and reused across probes; IConnection is cached by the
        // health-check infra so we don't open a new connection on every probe.
        var factory = new ConnectionFactory { Uri = new Uri(rabbitUri) };
        builder.AddRabbitMQ(
            sp => factory.CreateConnectionAsync(),
            name: "rabbitmq",
            tags: [ReadyTag]);

        return services;
    }

    /// <summary>Maps /health/live and /health/ready with JSON responses.</summary>
    public static IEndpointRouteBuilder MapMarketplaceHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = _ => false, // No checks run — just confirms process is up.
            ResponseWriter = HealthCheckJson.Write,
        }).AllowAnonymous();

        endpoints.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag),
            ResponseWriter = HealthCheckJson.Write,
        }).AllowAnonymous();

        return endpoints;
    }
}
