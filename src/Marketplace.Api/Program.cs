using Marketplace.Api.Authorization;
using Marketplace.Api.Configuration;
using Marketplace.Api.Endpoints;
using Marketplace.Application.Abstractions;
using Marketplace.Application.Common;
using Marketplace.Infrastructure;
using Marketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, _, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddOptions<AppOptions>()
    .Bind(builder.Configuration.GetSection(AppOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is required");

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(connectionString);

var app = builder.Build();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    var options = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AppOptions>>();
    if (options.Value.SeedOnStartup)
    {
        await DataSeeder.SeedAsync(db);
    }
}

app.MapGet("/", () => "Marketplace API — Tier 2");
app.MapBuyerEndpoints();
app.MapSellerEndpoints();
app.MapAdminEndpoints();

await app.RunAsync();

public partial class Program;
