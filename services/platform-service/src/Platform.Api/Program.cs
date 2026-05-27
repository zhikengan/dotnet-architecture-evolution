using BuildingBlocks.Api;
using BuildingBlocks.Application;
using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Infrastructure.Telemetry;
using BuildingBlocks.Infrastructure.Time;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Platform.Api.Endpoints;
using Platform.Api.GrpcServices;
using Platform.Application.Abstractions;
using Platform.Application.FeatureFlags.Queries;
using Platform.Infrastructure.Messaging;
using Platform.Infrastructure.Persistence;
using Serilog;

const string ServiceName = "platform-service";

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .Enrich.FromLogContext()
    .Enrich.WithProperty("service", ServiceName)
    .WriteTo.Console());

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services.AddDbContext<PlatformDbContext>(opt =>
{
    var cs = builder.Configuration.GetConnectionString("Default")
        ?? "Host=localhost;Port=5437;Database=platform;Username=platform;Password=platform";
    opt.UseNpgsql(cs, npg => npg.MigrationsHistoryTable("__ef_migrations", PlatformDbContext.Schema));
});
builder.Services.AddScoped<IPlatformDbContext>(sp => sp.GetRequiredService<PlatformDbContext>());

builder.Services.AddPlatformMessaging(builder.Configuration);
builder.Services.AddMarketplaceObservability(builder.Configuration, ServiceName);

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<IsFeatureEnabledQuery>();
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

builder.Services.AddGrpc();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    await db.Database.EnsureCreatedAsync();
    var clock = scope.ServiceProvider.GetRequiredService<IClock>();
    await PlatformDataSeeder.SeedAsync(db, clock);
}

app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = ServiceName }));
app.MapAdminEndpoints();
app.MapGrpcService<PlatformGrpcService>();

await app.RunAsync();

public partial class Program;
