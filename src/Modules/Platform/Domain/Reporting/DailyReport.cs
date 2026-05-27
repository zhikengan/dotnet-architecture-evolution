using BuildingBlocks.Domain;
using BuildingBlocks.Domain.MultiTenancy;

namespace Platform.Domain.Reporting;

/// <summary>
/// Per-tenant daily summary written by the Worker's <c>DailyReportingJob</c>.
/// Tenant-scoped because reports MUST be partitioned (a tenant must never see
/// another's revenue). Composite key on (TenantId, Date).
/// </summary>
public sealed class DailyReport : Entity<Guid>, IMultiTenant
{
    public Guid TenantId { get; private set; }
    public DateOnly Date { get; private set; }
    public int TotalOrders { get; private set; }
    public int ConfirmedOrders { get; private set; }
    public int CancelledOrders { get; private set; }
    public decimal TotalRevenue { get; private set; }
    public DateTime ComputedAt { get; private set; }

    private DailyReport() { }

    public static DailyReport Create(Guid tenantId, DateOnly date, int total, int confirmed, int cancelled, decimal revenue, DateTime now) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Date = date,
            TotalOrders = total,
            ConfirmedOrders = confirmed,
            CancelledOrders = cancelled,
            TotalRevenue = revenue,
            ComputedAt = now,
        };
}
