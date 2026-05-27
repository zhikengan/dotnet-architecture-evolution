using BuildingBlocks.Application;
using BuildingBlocks.Application.MultiTenancy;
using BuildingBlocks.Infrastructure.EventBus;
using BuildingBlocks.Infrastructure.MultiTenancy;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Telemetry;
using BuildingBlocks.Infrastructure.Time;
using Catalog;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Orders;
using Platform;
using Serilog;

// The Worker is a WebApplication (not a bare IHost) only because Hangfire's
// dashboard needs HTTP routing — see HangfireConfiguration. The Worker has
// NO controllers, NO MVC, NO auth surface; it's enforced by an architecture
// test. Outbox dispatch, scheduled jobs, and fire-and-forget background work
// run here so the API host can scale on its own request profile.
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, _, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .WriteTo.Console());

// Cross-cutting services — same shape as the API host.
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IEventBus, InMemoryEventBus>();

// Tenant context (scoped) — set per-outbox-message by InMemoryEventBus before
// dispatch so handlers see the right tenant.
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
builder.Services.AddScoped<ITenantContextSetter>(sp => sp.GetRequiredService<TenantContext>());

// Modules — registered identically to the API host. The Worker has access to
// every module's DbContext + integration-event handlers; it just doesn't map
// any HTTP routes.
builder.Services.AddPlatformModule(builder.Configuration);
builder.Services.AddCatalogModule(builder.Configuration);
builder.Services.AddOrdersModule(builder.Configuration);

// Outbox processor lives here, not in the API host (Tier 3's location).
builder.Services.Configure<OutboxOptions>(builder.Configuration.GetSection(OutboxOptions.SectionName));
builder.Services.AddHostedService<OutboxProcessor>();

// OpenTelemetry — same exports as the API host so traces span both processes.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(builder.Configuration["OTEL_SERVICE_NAME"] ?? "marketplace-worker"))
    .WithTracing(t => t
        .AddSource(MarketplaceActivitySource.Name)
        .AddAspNetCoreInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(m => m
        .AddMeter(MarketplaceActivitySource.Name)
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter());

var app = builder.Build();

app.UseSerilogRequestLogging();

// Minimal liveness endpoint. The Worker does NOT mount any business routes.
app.MapGet("/health", () => Results.Ok(new { status = "up" }));

await app.RunAsync();

public partial class Program;
