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

builder.Services.AddDbContext<IdentityDbContext>((sp, opt) =>
{
    var cs = builder.Configuration.GetConnectionString("Default")
        ?? "Host=localhost;Port=5435;Database=identity;Username=identity;Password=identity";
    opt.UseNpgsql(cs, npg => npg.MigrationsHistoryTable("__ef_migrations", IdentityDbContext.Schema));
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

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await db.Database.EnsureCreatedAsync();
    var clock = scope.ServiceProvider.GetRequiredService<IClock>();
    await IdentityDataSeeder.SeedAsync(db, clock);
}

app.UseSerilogRequestLogging();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = ServiceName }));
app.MapDemoTokenEndpoints();
app.MapGrpcService<IdentityGrpcService>();

await app.RunAsync();

public partial class Program;
