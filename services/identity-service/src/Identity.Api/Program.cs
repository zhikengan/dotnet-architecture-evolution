using BuildingBlocks.Api.HealthChecks;
using BuildingBlocks.Application;
using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Infrastructure.Telemetry;
using BuildingBlocks.Infrastructure.Time;
using Identity.Api.Endpoints;
using Identity.Api.GrpcServices;
using Identity.Application.Abstractions;
using Identity.Application.Authentication;
using Identity.Application.Users.Queries;
using Identity.Infrastructure.Authentication;
using Identity.Infrastructure.Messaging;
using Identity.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Serilog;

const string ServiceName = "identity-service";

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .Enrich.FromLogContext()
    .Enrich.WithProperty("service", ServiceName)
    .WriteTo.Console());

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton<IJwtTokenIssuer, JwtTokenIssuer>();

var pgConnectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Port=5435;Database=identity;Username=identity;Password=identity";
builder.Services.AddDbContext<IdentityDbContext>((sp, opt) =>
{
    opt.UseNpgsql(pgConnectionString, npg => npg.MigrationsHistoryTable("__ef_migrations", IdentityDbContext.Schema));
});
builder.Services.AddScoped<IIdentityDbContext>(sp => sp.GetRequiredService<IdentityDbContext>());

builder.Services.AddIdentityMessaging(builder.Configuration);
builder.Services.AddMarketplaceObservability(builder.Configuration, ServiceName);

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<IssueDemoTokenQuery>();
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
});

builder.Services.AddGrpc();
builder.Services.AddMarketplaceHealthChecks(builder.Configuration, pgConnectionString);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await db.Database.MigrateAsync();
    var clock = scope.ServiceProvider.GetRequiredService<IClock>();
    await IdentityDataSeeder.SeedAsync(db, clock);
}

app.UseSerilogRequestLogging();

app.MapMarketplaceHealthChecks();
app.MapDemoTokenEndpoints();
app.MapGrpcService<IdentityGrpcService>();

await app.RunAsync();

public partial class Program;
