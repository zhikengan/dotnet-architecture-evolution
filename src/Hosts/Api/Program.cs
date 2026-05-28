using BuildingBlocks.Api;
using BuildingBlocks.Application;
using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Infrastructure.EventBus;
using BuildingBlocks.Infrastructure.Storage;
using BuildingBlocks.Infrastructure.Telemetry;
using BuildingBlocks.Infrastructure.Time;
using Catalog;
using Catalog.Infrastructure.Persistence;
using Marketplace.Api.Authentication;
using Marketplace.Api.Endpoints;
using Marketplace.Api.Endpoints.Demo;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Orders;
using Orders.Infrastructure.Persistence;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Platform;
using Platform.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, _, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .WriteTo.Console());

// Cross-cutting services
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<ICorrelationContext, CorrelationContext>();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddSingleton<IEventBus, InMemoryEventBus>();

// Authentication + authorization (JwtBearer + role policies; shared via BuildingBlocks)
builder.Services.AddMarketplaceAuthentication(builder.Configuration);

// File storage (S3 via MinIO in docker-compose; LocalFileStorage fallback in tests)
builder.Services.AddMarketplaceStorage(builder.Configuration);

// Per-user rate limiting (10 writes/min, 100 reads/min) — applied to endpoint groups below.
builder.Services.AddMarketplaceRateLimiting(builder.Configuration);

// Outbox processor moved out at Tier 4 — runs in src/Hosts/Worker now.
// API host still publishes outbox rows but no longer dispatches them.

// Open-generic MediatR pipeline behaviors. Registered AT THE HOST so they
// run for every module's commands/queries.
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CorrelationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(IdempotencyBehavior<,>));

// Modules
builder.Services.AddPlatformModule(builder.Configuration);
builder.Services.AddCatalogModule(builder.Configuration);
builder.Services.AddOrdersModule(builder.Configuration);

// OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(builder.Configuration["OTEL_SERVICE_NAME"] ?? "marketplace"))
    .WithTracing(t => t
        .AddSource(MarketplaceActivitySource.Name)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(m => m
        .AddMeter(MarketplaceActivitySource.Name)
        .AddMeter(MarketplaceMeter.Name)
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter());

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseMarketplaceSecurityHeaders();
app.UseMiddleware<CorrelationMiddleware>();
app.UseAuthentication();
app.UseMiddleware<TenantMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();

// Migrate + seed in Development only (forbidden in production paths per Tier 3 rules).
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var platformDb = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    var catalogDb = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    var ordersDb = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
    await platformDb.Database.MigrateAsync();
    await catalogDb.Database.MigrateAsync();
    await ordersDb.Database.MigrateAsync();
    await PlatformDataSeeder.SeedAsync(platformDb);
    await CatalogDataSeeder.SeedAsync(catalogDb);

    // Demo token issuer — Development ONLY. There's no login or password;
    // the caller asserts (role, tenant, userId) and gets a real RS256 JWT
    // signed by the issuer the JwtBearer middleware validates against.
    app.MapDemoTokenEndpoint();
}

app.MapGet("/", () => "Marketplace API — Tier 4 (platform)");
app.MapGet("/health/live", () => Results.Ok("live"));
app.MapGet("/health/ready", () => Results.Ok("ready"));

// OIDC-style discovery endpoints — always mounted; safe to expose because
// they publish ONLY the public key, never the signing material.
app.MapDiscoveryEndpoints();

app.MapBuyerEndpoints();
app.MapSellerEndpoints();
app.MapAdminEndpoints();

await app.RunAsync();

public partial class Program;
