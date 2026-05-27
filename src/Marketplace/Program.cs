using Marketplace.Data;
using Marketplace.Endpoints;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.MapGet("/", () => "Marketplace MVP — Tier 1");
app.MapSellerEndpoints();
app.MapBuyerEndpoints();
app.MapAdminEndpoints();

app.Run();

public partial class Program;
