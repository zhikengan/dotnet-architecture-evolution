var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "Marketplace API — Tier 2");

app.Run();

public partial class Program;
