var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapGet("/", () => "Marketplace API — Tier 3 (modular monolith)");
app.Run();

public partial class Program;
