using Marketplace.Application.Common;
using Marketplace.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Database=marketplace;Username=marketplace;Password=marketplace";

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(connectionString);

var app = builder.Build();

app.MapGet("/", () => "Marketplace API — Tier 2");

app.Run();

public partial class Program;
