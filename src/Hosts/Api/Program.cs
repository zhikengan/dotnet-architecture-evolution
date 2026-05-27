using BuildingBlocks.Api;
using BuildingBlocks.Application;
using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Infrastructure.EventBus;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Telemetry;
using BuildingBlocks.Infrastructure.Time;
using Catalog;
using Catalog.Infrastructure.Persistence;
using Marketplace.Api.Authentication;
using Marketplace.Api.Endpoints;
using Marketplace.Api.Endpoints.Dev;
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

// Outbox processor
builder.Services.Configure<OutboxOptions>(builder.Configuration.GetSection(OutboxOptions.SectionName));
builder.Services.AddHostedService<OutboxProcessor>();

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
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter());

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseMiddleware<CorrelationMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

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

    // Dev-only token mint — registered ONLY in Development so the unauth
    // mint path can't ship to production.
    app.MapDevTokenEndpoints();
}

app.MapGet("/", () => "Marketplace API — Tier 3 (modular monolith)");
app.MapGet("/health/live", () => Results.Ok("live"));
app.MapGet("/health/ready", () => Results.Ok("ready"));

app.MapBuyerEndpoints();
app.MapSellerEndpoints();
app.MapAdminEndpoints();

await app.RunAsync();

public partial class Program;
