using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Domain.Reporting;

namespace Platform.Infrastructure.Persistence.Configurations;

public sealed class DailyReportConfiguration : IEntityTypeConfiguration<DailyReport>
{
    public void Configure(EntityTypeBuilder<DailyReport> e)
    {
        e.ToTable("daily_reports");
        e.HasKey(r => r.Id);
        e.Property(r => r.Id).HasColumnName("id");
        e.Property(r => r.TenantId).HasColumnName("tenant_id");
        e.Property(r => r.Date).HasColumnName("date");
        e.Property(r => r.TotalOrders).HasColumnName("total_orders");
        e.Property(r => r.ConfirmedOrders).HasColumnName("confirmed_orders");
        e.Property(r => r.CancelledOrders).HasColumnName("cancelled_orders");
        e.Property(r => r.TotalRevenue).HasColumnName("total_revenue").HasColumnType("numeric(18,2)");
        e.Property(r => r.ComputedAt).HasColumnName("computed_at");
        e.HasIndex(r => new { r.TenantId, r.Date }).IsUnique();
    }
}
