using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Telemetry;
using BuildingBlocks.Infrastructure.Time;
using Catalog.Application.Abstractions;
using Catalog.Infrastructure.Messaging;
using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Serilog;

const string ServiceName = "catalog-worker";

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog(lc => lc
    .Enrich.FromLogContext()
    .Enrich.WithProperty("service", ServiceName)
    .WriteTo.Console());

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<ITenantContext, TenantContext>();

builder.Services.AddDbContext<CatalogDbContext>(opt =>
{
    var cs = builder.Configuration.GetConnectionString("Default")
        ?? "Host=localhost;Port=5433;Database=catalog;Username=catalog;Password=catalog";
    opt.UseNpgsql(cs);
});
builder.Services.AddScoped<ICatalogDbContext>(sp => sp.GetRequiredService<CatalogDbContext>());

// Worker also registers the saga consumers — RabbitMQ load-balances messages
// across API and Worker instances on the same queue.
builder.Services.AddCatalogMessaging(builder.Configuration, registerConsumers: true);
builder.Services.AddMarketplaceObservability(builder.Configuration, ServiceName);

var host = builder.Build();
await host.RunAsync();
