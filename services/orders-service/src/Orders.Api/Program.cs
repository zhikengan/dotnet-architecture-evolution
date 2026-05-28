using BuildingBlocks.Api;
using BuildingBlocks.Api.HealthChecks;
using BuildingBlocks.Application;
using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Infrastructure.Telemetry;
using BuildingBlocks.Infrastructure.Time;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Orders.Api.Endpoints;
using Orders.Api.GrpcServices;
using Orders.Application.Abstractions;
using Orders.Application.Orders.PlaceOrder;
using Orders.Infrastructure.Messaging;
using Orders.Infrastructure.Persistence;
using Serilog;

const string ServiceName = "orders-service";

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .Enrich.FromLogContext()
    .Enrich.WithProperty("service", ServiceName)
    .WriteTo.Console());

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

var pgConnectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Port=5434;Database=orders;Username=orders;Password=orders";
builder.Services.AddDbContext<OrdersDbContext>((sp, opt) =>
{
    opt.UseNpgsql(pgConnectionString, npg => npg.MigrationsHistoryTable("__ef_migrations", OrdersDbContext.Schema));
});
builder.Services.AddScoped<IOrdersDbContext>(sp => sp.GetRequiredService<OrdersDbContext>());

builder.Services.AddOrdersMessaging(builder.Configuration, registerConsumers: true);
builder.Services.AddMarketplaceObservability(builder.Configuration, ServiceName);

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<PlaceOrderCommand>();
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(AuthorizationBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});
builder.Services.AddValidatorsFromAssemblyContaining<PlaceOrderValidator>();

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
    opt.AddPolicy("buyer", p => p.RequireClaim("role", "Buyer"));
    opt.AddPolicy("admin", p => p.RequireClaim("role", "Admin"));
});

builder.Services.AddGrpc();
builder.Services.AddMarketplaceHealthChecks(builder.Configuration, pgConnectionString);

var app = builder.Build();

// Migrate in Development only. Production applies migration bundles out-of-band
// (see docs/runbooks/migrations.md) so concurrent instances don't race.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
    await db.Database.MigrateAsync();
}

app.UseSerilogRequestLogging();
app.UseMiddleware<CorrelationMiddleware>();
app.UseAuthentication();
app.UseMiddleware<TenantMiddleware>();
app.UseAuthorization();

app.MapMarketplaceHealthChecks();
app.MapBuyerOrderEndpoints();
app.MapAdminOrderEndpoints();
app.MapGrpcService<OrdersGrpcService>();

await app.RunAsync();

public partial class Program;
