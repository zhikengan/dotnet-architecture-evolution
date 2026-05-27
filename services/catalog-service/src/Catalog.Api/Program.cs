using BuildingBlocks.Api;
using BuildingBlocks.Application;
using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Infrastructure.Telemetry;
using BuildingBlocks.Infrastructure.Time;
using Catalog.Api.Endpoints;
using Catalog.Api.GrpcServices;
using Catalog.Application.Abstractions;
using Catalog.Application.Products.CreateProduct;
using Catalog.Infrastructure.Messaging;
using Catalog.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

const string ServiceName = "catalog-service";

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .Enrich.FromLogContext()
    .Enrich.WithProperty("service", ServiceName)
    .WriteTo.Console());

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services.AddDbContext<CatalogDbContext>((sp, opt) =>
{
    var cs = builder.Configuration.GetConnectionString("Default")
        ?? "Host=localhost;Port=5433;Database=catalog;Username=catalog;Password=catalog";
    opt.UseNpgsql(cs, npg => npg.MigrationsHistoryTable("__ef_migrations", CatalogDbContext.Schema));
});
builder.Services.AddScoped<ICatalogDbContext>(sp => sp.GetRequiredService<CatalogDbContext>());

builder.Services.AddCatalogMessaging(builder.Configuration, registerConsumers: true);
builder.Services.AddMarketplaceObservability(builder.Configuration, ServiceName);

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<CreateProductCommand>();
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(AuthorizationBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});
builder.Services.AddValidatorsFromAssemblyContaining<CreateProductValidator>();

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
    opt.AddPolicy("seller", p => p.RequireClaim("role", "Seller"));
    opt.AddPolicy("admin", p => p.RequireClaim("role", "Admin"));
});

builder.Services.AddGrpc();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    await db.Database.EnsureCreatedAsync();
    var clock = scope.ServiceProvider.GetRequiredService<IClock>();
    await CatalogDataSeeder.SeedAsync(db, clock);
}

app.UseSerilogRequestLogging();
app.UseMiddleware<CorrelationMiddleware>();
app.UseAuthentication();
app.UseMiddleware<TenantMiddleware>();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = ServiceName }));
app.MapSellerEndpoints();
app.MapBuyerEndpoints();
app.MapAdminEndpoints();
app.MapGrpcService<CatalogGrpcService>();

await app.RunAsync();

public partial class Program;
