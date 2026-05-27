using Marketplace.Worker.ScheduledJobs;
using Quartz;

namespace Marketplace.Worker.Configuration;

/// <summary>
/// Quartz wiring. Uses the in-memory job store by default; production hosts
/// can flip to <c>UsePersistentStore</c> against the Postgres <c>quartz</c>
/// schema via Quartz.NET's bundled SQL. The pattern of "deterministic
/// schedules + cron triggers" is the Tier-4 lesson — the persistence
/// choice is an operational one.
/// </summary>
public static class QuartzConfiguration
{
    public static IServiceCollection AddMarketplaceQuartz(this IServiceCollection services, IHostEnvironment env)
    {
        services.AddQuartz(q =>
        {
            var expireKey = new JobKey("ExpireStaleOrders");
            q.AddJob<ExpireStaleOrdersJob>(opts => opts.WithIdentity(expireKey));
            q.AddTrigger(t => t
                .ForJob(expireKey)
                .WithIdentity("ExpireStaleOrders-trigger")
                // Dev runs every 5 minutes so the demo is visible without
                // waiting overnight; prod cadence is "every 1 minute".
                .WithCronSchedule(env.IsDevelopment() ? "0 */5 * * * ?" : "0 */1 * * * ?"));

            var reportKey = new JobKey("DailyReporting");
            q.AddJob<DailyReportingJob>(opts => opts.WithIdentity(reportKey));
            q.AddTrigger(t => t
                .ForJob(reportKey)
                .WithIdentity("DailyReporting-trigger")
                .WithCronSchedule("0 0 2 * * ?")); // 02:00 UTC daily
        });

        services.AddQuartzHostedService(opts =>
        {
            opts.WaitForJobsToComplete = true;
        });

        return services;
    }
}
