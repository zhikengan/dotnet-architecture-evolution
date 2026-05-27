using Hangfire;

namespace Marketplace.Worker.Configuration;

/// <summary>
/// Hangfire server + dashboard. Client + storage are registered by the
/// Platform module (so both API and Worker can enqueue against the same
/// backing store); only the Worker runs the server that picks jobs up
/// and a dashboard at <c>/hangfire</c>.
/// </summary>
public static class HangfireConfiguration
{
    public static IServiceCollection AddMarketplaceHangfireServer(this IServiceCollection services)
    {
        services.AddHangfireServer(opts =>
        {
            opts.WorkerCount = Math.Max(Environment.ProcessorCount, 2);
            opts.SchedulePollingInterval = TimeSpan.FromSeconds(5);
        });
        return services;
    }

    public static WebApplication UseMarketplaceHangfireDashboard(this WebApplication app)
    {
        // Mounting the dashboard is what forces the Worker to be a WebApplication
        // rather than a bare IHost. The Worker still asserts "no MVC" via an
        // architecture test — the dashboard renders via its own middleware.
        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            DashboardTitle = "Marketplace Worker — Hangfire",
            Authorization = [], // dev only — production fronts this with an Admin filter
        });
        return app;
    }
}
