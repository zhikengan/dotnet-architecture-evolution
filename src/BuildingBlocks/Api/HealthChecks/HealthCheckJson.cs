using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BuildingBlocks.Api.HealthChecks;

/// <summary>
/// JSON response writer for health-check endpoints. Default ASP.NET writer is
/// plain text (just "Healthy"/"Degraded"); operators benefit from per-check
/// details when something falls over.
/// </summary>
public static class HealthCheckJson
{
    public static Task Write(HttpContext ctx, HealthReport report)
    {
        ctx.Response.ContentType = "application/json";
        var payload = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                durationMs = e.Value.Duration.TotalMilliseconds,
            }),
        };
        return ctx.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
