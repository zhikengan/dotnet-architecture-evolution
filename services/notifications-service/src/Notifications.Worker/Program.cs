using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Telemetry;
using BuildingBlocks.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Notifications.Application.Abstractions;
using Notifications.Infrastructure.Messaging;
using Notifications.Infrastructure.Persistence;
using Serilog;

const string ServiceName = "notifications-worker";

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog(lc => lc
    .Enrich.FromLogContext()
    .Enrich.WithProperty("service", ServiceName)
    .WriteTo.Console());

builder.Services.AddSingleton<IClock, SystemClock>();

builder.Services.AddDbContext<NotificationsDbContext>(opt =>
{
    var cs = builder.Configuration.GetConnectionString("Default")
        ?? "Host=localhost;Port=5436;Database=notifications;Username=notifications;Password=notifications";
    opt.UseNpgsql(cs);
});
builder.Services.AddScoped<INotificationsDbContext>(sp => sp.GetRequiredService<NotificationsDbContext>());

builder.Services.AddNotificationsMessaging(builder.Configuration, registerConsumers: true);
builder.Services.AddMarketplaceObservability(builder.Configuration, ServiceName);

var host = builder.Build();
await host.RunAsync();
