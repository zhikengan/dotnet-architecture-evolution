using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Telemetry;
using BuildingBlocks.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Orders.Application.Abstractions;
using Orders.Infrastructure.Messaging;
using Orders.Infrastructure.Persistence;
using Serilog;

const string ServiceName = "orders-worker";

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog(lc => lc
    .Enrich.FromLogContext()
    .Enrich.WithProperty("service", ServiceName)
    .WriteTo.Console());

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<ITenantContext, TenantContext>();

builder.Services.AddDbContext<OrdersDbContext>(opt =>
{
    var cs = builder.Configuration.GetConnectionString("Default")
        ?? "Host=localhost;Port=5434;Database=orders;Username=orders;Password=orders";
    opt.UseNpgsql(cs);
});
builder.Services.AddScoped<IOrdersDbContext>(sp => sp.GetRequiredService<OrdersDbContext>());

builder.Services.AddOrdersMessaging(builder.Configuration, registerConsumers: true);
builder.Services.AddMarketplaceObservability(builder.Configuration, ServiceName);

var host = builder.Build();
await host.RunAsync();
