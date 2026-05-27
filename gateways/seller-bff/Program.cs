using BuildingBlocks.Api;
using BuildingBlocks.Infrastructure.Telemetry;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Yarp.ReverseProxy.Transforms;

const string ServiceName = "seller-bff";

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .Enrich.FromLogContext()
    .Enrich.WithProperty("service", ServiceName)
    .WriteTo.Console());

builder.Services.AddMarketplaceObservability(builder.Configuration, ServiceName);

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

builder.Services.AddAuthorization();

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(ctx => ctx.AddRequestTransform(reqCtx =>
    {
        if (reqCtx.HttpContext.Request.Headers.TryGetValue("Authorization", out var auth))
            reqCtx.ProxyRequest.Headers.TryAddWithoutValidation("Authorization", (string[])auth!);
        return ValueTask.CompletedTask;
    }));

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseMiddleware<CorrelationMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = ServiceName }));
app.MapReverseProxy();

await app.RunAsync();

public partial class Program;
