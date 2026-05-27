using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Time;
using Platform;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<ICorrelationContext, CorrelationContext>();

builder.Services.AddPlatformModule(builder.Configuration);

var app = builder.Build();
app.MapGet("/", () => "Marketplace API — Tier 3 (modular monolith)");
app.Run();

public partial class Program;
