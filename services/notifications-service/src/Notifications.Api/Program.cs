using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Telemetry;
using BuildingBlocks.Infrastructure.Time;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Notifications.Application.Abstractions;
using Notifications.Infrastructure.Messaging;
using Notifications.Infrastructure.Persistence;
using Serilog;

const string ServiceName = "notifications-service";

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .Enrich.FromLogContext()
    .Enrich.WithProperty("service", ServiceName)
    .WriteTo.Console());

builder.Services.AddSingleton<IClock, SystemClock>();

builder.Services.AddDbContext<NotificationsDbContext>(opt =>
{
    var cs = builder.Configuration.GetConnectionString("Default")
        ?? "Host=localhost;Port=5436;Database=notifications;Username=notifications;Password=notifications";
    opt.UseNpgsql(cs, npg => npg.MigrationsHistoryTable("__ef_migrations", NotificationsDbContext.Schema));
});
builder.Services.AddScoped<INotificationsDbContext>(sp => sp.GetRequiredService<NotificationsDbContext>());

builder.Services.AddNotificationsMessaging(builder.Configuration, registerConsumers: true);
builder.Services.AddMarketplaceObservability(builder.Configuration, ServiceName);

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

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = ServiceName }));

app.MapGet("/admin/notifications/by-order/{orderId:guid}", async (Guid orderId, INotificationsDbContext db, CancellationToken ct) =>
{
    var rows = await db.Notifications.AsNoTracking()
        .Where(n => n.RelatedOrderId == orderId)
        .OrderByDescending(n => n.SentAt)
        .Select(n => new { id = n.Id.Value, tenantId = n.TenantId, type = n.Type, recipient = n.Recipient, body = n.Body, sentAt = n.SentAt })
        .ToListAsync(ct);
    return Results.Ok(rows);
}).RequireAuthorization("admin");

await app.RunAsync();

public partial class Program;
