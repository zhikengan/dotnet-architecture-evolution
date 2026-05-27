using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Telemetry;
using BuildingBlocks.Infrastructure.Time;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Platform.Api.Endpoints;
using Platform.Api.GrpcServices;
using Platform.Application.Abstractions;
using Platform.Infrastructure.Messaging;
using Platform.Infrastructure.Persistence;
using Serilog;

const string ServiceName = "platform-service";

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .Enrich.FromLogContext()
    .Enrich.WithProperty("service", ServiceName)
    .WriteTo.Console());

builder.Services.AddSingleton<IClock, SystemClock>();

builder.Services.AddDbContext<PlatformDbContext>(opt =>
{
    var cs = builder.Configuration.GetConnectionString("Default")
        ?? "Host=localhost;Port=5437;Database=platform;Username=platform;Password=platform";
    opt.UseNpgsql(cs, npg => npg.MigrationsHistoryTable("__ef_migrations", PlatformDbContext.Schema));
});
builder.Services.AddScoped<IPlatformDbContext>(sp => sp.GetRequiredService<PlatformDbContext>());

builder.Services.AddPlatformMessaging(builder.Configuration);
builder.Services.AddMarketplaceObservability(builder.Configuration, ServiceName);

var jwksUrl = builder.Configuration["Identity:JwksUrl"] ?? "http://localhost:5300/.well-known/jwks.json";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.Authority = builder.Configuration["Identity:Authority"] ?? "http://localhost:5300";
        o.RequireHttpsMetadata = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidIssuer = "marketplace-identity",
            ValidAudience = "marketplace",
        };
        o.MetadataAddress = (builder.Configuration["Identity:Authority"] ?? "http://localhost:5300") + "/.well-known/openid-configuration";
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
