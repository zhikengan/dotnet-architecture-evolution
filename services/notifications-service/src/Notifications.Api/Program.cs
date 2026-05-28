using BuildingBlocks.Api;
using BuildingBlocks.Api.HealthChecks;
using BuildingBlocks.Application;
using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Infrastructure.Telemetry;
using BuildingBlocks.Infrastructure.Time;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Notifications.Api.Endpoints;
using Notifications.Application.Abstractions;
using Notifications.Application.Notifications.Queries;
using Notifications.Infrastructure.Messaging;
using Notifications.Infrastructure.Persistence;
using Serilog;

const string ServiceName = "notifications-service";

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .Enrich.FromLogContext()
    .Enrich.WithProperty("service", ServiceName)
    .WriteTo.Console());

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

var pgConnectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Port=5436;Database=notifications;Username=notifications;Password=notifications";
builder.Services.AddDbContext<NotificationsDbContext>(opt =>
{
    opt.UseNpgsql(pgConnectionString, npg => npg.MigrationsHistoryTable("__ef_migrations", NotificationsDbContext.Schema));
});
builder.Services.AddScoped<INotificationsDbContext>(sp => sp.GetRequiredService<NotificationsDbContext>());

builder.Services.AddNotificationsMessaging(builder.Configuration, registerConsumers: true);
builder.Services.AddMarketplaceObservability(builder.Configuration, ServiceName);

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<ListNotificationsByOrderQuery>();
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.RequireHttpsMetadata = false;
        o.MetadataAddress = (builder.Configuration["Identity:Authority"] ?? "http://localhost:5300") + "/.well-known/openid-configuration";
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidIssuer = "marketplace-identity",
            ValidAudience = "marketplace",
        };
    });

builder.Services.AddAuthorization(opt =>
{
    opt.AddPolicy("admin", p => p.RequireClaim("role", "Admin"));
});

builder.Services.AddMarketplaceHealthChecks(builder.Configuration, pgConnectionString);

var app = builder.Build();

// Migrate in Development only. Production applies migration bundles out-of-band
// (see docs/runbooks/migrations.md) so concurrent instances don't race.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
    await db.Database.MigrateAsync();
}

app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();

app.MapMarketplaceHealthChecks();
app.MapAdminNotificationEndpoints();

await app.RunAsync();

public partial class Program;
