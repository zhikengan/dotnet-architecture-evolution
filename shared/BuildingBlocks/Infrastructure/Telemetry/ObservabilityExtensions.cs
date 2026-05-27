using MassTransit.Logging;
using MassTransit.Monitoring;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

namespace BuildingBlocks.Infrastructure.Telemetry;

public static class ObservabilityExtensions
{
    public static IServiceCollection AddMarketplaceObservability(
        this IServiceCollection services,
        IConfiguration config,
        string serviceName)
    {
        var otlpEndpoint = config["Otel:Endpoint"] ?? "http://localhost:4317";

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName: serviceName, serviceVersion: "1.0.0"))
            .WithTracing(t => t
                .AddSource(MarketplaceActivitySource.Name)
                .AddSource(DiagnosticHeaders.DefaultListenerName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddGrpcClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)));

        return services;
    }

    public static void ConfigureSerilog(this IConfiguration config, string serviceName, LoggerConfiguration logger)
    {
        logger
            .Enrich.FromLogContext()
            .Enrich.WithProperty("service", serviceName)
            .WriteTo.Console();
    }
}
