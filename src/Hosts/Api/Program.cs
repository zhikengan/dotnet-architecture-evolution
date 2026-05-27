using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.EventBus;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Time;
using Catalog;
using Orders;
using Platform;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<ICorrelationContext, CorrelationContext>();
builder.Services.AddSingleton<IEventBus, InMemoryEventBus>();
builder.Services.Configure<OutboxOptions>(builder.Configuration.GetSection(OutboxOptions.SectionName));
builder.Services.AddHostedService<OutboxProcessor>();

builder.Services.AddPlatformModule(builder.Configuration);
builder.Services.AddCatalogModule(builder.Configuration);
builder.Services.AddOrdersModule(builder.Configuration);

var app = builder.Build();
app.MapGet("/", () => "Marketplace API — Tier 3 (modular monolith)");
app.Run();

public partial class Program;
